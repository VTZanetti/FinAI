using FinAI.Api.Common;
using FinAI.Api.Repositories;
using FinAI.Api.Services.Forecasting.Models;

namespace FinAI.Api.Services.Forecasting;

public interface IForecastService
{
    Task<Result<CashFlowForecast>> GetCashFlowForecastAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Previsão de fluxo de caixa — média móvel ponderada por recência sobre séries mensais (FR-07).
/// Despesas recorrentes entram garantidas na base.
/// </summary>
public class ForecastService : IForecastService
{
    private const int MaxMonths = 24;
    private const string Method = "weighted_moving_average";

    private readonly IAnalyticsRepository _analytics;
    private readonly IMovingAverageForecaster _forecaster;
    private readonly ITransactionRepository _transactions;

    public ForecastService(
        IAnalyticsRepository analytics,
        IMovingAverageForecaster forecaster,
        ITransactionRepository transactions)
    {
        _analytics = analytics;
        _forecaster = forecaster;
        _transactions = transactions;
    }

    public async Task<Result<CashFlowForecast>> GetCashFlowForecastAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > MaxMonths)
            return Result.Failure<CashFlowForecast>(ErrorCode.Validation, $"months must be between 1 and {MaxMonths}");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var historyMonths = Math.Min(24, Math.Max(12, months * 2));
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(historyMonths - 1));
        var to = today;

        var series = await _analytics.GetMonthlyTotalsAsync(userId, from, to, accountId, cancellationToken);

        var incomeHistory = series.Select(s => s.Income).ToList();
        var expensesHistory = series.Select(s => s.Expenses).ToList();

        // Despesas recorrentes ativas como base garantida (soma mensal)
        var recurringMonthly = await GetRecurringMonthlyTotalAsync(userId, accountId, cancellationToken);

        var forecast = new List<MonthlyForecastPoint>();
        for (var i = 1; i <= months; i++)
        {
            var monthDate = new DateOnly(today.Year, today.Month, 1).AddMonths(i);

            // Prevê o próximo mês da série; depois desloca a janela (adiciona previsão ao histórico)
            var income = i == 1 ? _forecaster.ForecastNext(incomeHistory) : _forecaster.ForecastNext(incomeHistory);
            var expenses = i == 1 ? _forecaster.ForecastNext(expensesHistory) : _forecaster.ForecastNext(expensesHistory);

            // Recorrência garantida: não deixa a despesa prevista abaixo da base recorrente
            if (recurringMonthly > expenses)
                expenses = recurringMonthly;

            incomeHistory.Add(income);
            expensesHistory.Add(expenses);

            forecast.Add(new MonthlyForecastPoint(
                $"{monthDate.Year:0000}-{monthDate.Month:00}",
                Math.Round(income, 2),
                Math.Round(expenses, 2),
                Math.Round(income - expenses, 2)));
        }

        var confidence = BuildConfidence(series.Count, incomeHistory, expensesHistory);

        return Result.Success(new CashFlowForecast(Method, DateTimeOffset.UtcNow, forecast, confidence));
    }

    private async Task<decimal> GetRecurringMonthlyTotalAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-2);
        var to = today;

        var recurring = await _analytics.GetRecurringExpensesAsync(userId, from, to, accountId, cancellationToken);
        return Math.Round(recurring / 3m, 2); // média mensal das recorrentes dos últimos 3 meses
    }

    private static ConfidenceInfo BuildConfidence(int historyCount, IReadOnlyList<decimal> income, IReadOnlyList<decimal> expenses)
    {
        var level = historyCount switch
        {
            >= 12 => "high",
            >= 6 => "medium",
            _ => "low"
        };

        return new ConfidenceInfo(level, $"Baseado em {historyCount} meses de histórico");
    }
}
