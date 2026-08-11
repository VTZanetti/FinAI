using FinAI.Api.Repositories;
using FinAI.Api.Services.Analytics.Models;

namespace FinAI.Api.Services.Analytics;

public interface IMonthlyTrendAnalyzer
{
    Task<MonthlyTrendReport> AnalyzeAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Série mensal contínua de receitas/despesas — meses sem dados aparecem zerados.
/// </summary>
public class MonthlyTrendAnalyzer : IMonthlyTrendAnalyzer
{
    private readonly IAnalyticsRepository _repository;

    public MonthlyTrendAnalyzer(IAnalyticsRepository repository)
    {
        _repository = repository;
    }

    public async Task<MonthlyTrendReport> AnalyzeAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var to = today;

        var aggregates = await _repository.GetMonthlyTotalsAsync(userId, from, to, accountId, cancellationToken);
        var aggregateMap = aggregates.ToDictionary(a => (a.Year, a.Month));

        var points = new List<MonthlyTrendPoint>();
        for (var i = 0; i < months; i++)
        {
            var monthDate = new DateOnly(from.Year, from.Month, 1).AddMonths(i);
            aggregateMap.TryGetValue((monthDate.Year, monthDate.Month), out var agg);

            var income = agg?.Income ?? 0m;
            var expenses = agg?.Expenses ?? 0m;

            points.Add(new MonthlyTrendPoint(
                $"{monthDate.Year:0000}-{monthDate.Month:00}",
                Math.Round(income, 2),
                Math.Round(expenses, 2),
                Math.Round(income - expenses, 2)));
        }

        return new MonthlyTrendReport(points, new PeriodInfo(from, to), DateTimeOffset.UtcNow);
    }
}
