using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services.Analytics.Models;

namespace FinAI.Api.Services.Analytics;

public interface IBehaviorAnalyzer
{
    Task<BehaviorReport> AnalyzeAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Variação de gastos por categoria e insights de comportamento (FR-06).
/// Compara a janela atual (últimos N meses) com a janela anterior (N meses antes).
/// </summary>
public class BehaviorAnalyzer : IBehaviorAnalyzer
{
    private const decimal VariationThresholdPercent = 10m;

    private readonly IAnalyticsRepository _repository;

    public BehaviorAnalyzer(IAnalyticsRepository repository)
    {
        _repository = repository;
    }

    public async Task<BehaviorReport> AnalyzeAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentFrom = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var currentTo = today;
        var previousFrom = currentFrom.AddMonths(-months);
        var previousTo = currentFrom.AddDays(-1);

        var currentByCategory = await _repository.GetExpensesByCategoryAsync(userId, currentFrom, currentTo, accountId, cancellationToken);
        var previousByCategory = await _repository.GetExpensesByCategoryAsync(userId, previousFrom, previousTo, accountId, cancellationToken);

        var currentTotals = await _repository.GetTotalsAsync(userId, currentFrom, currentTo, accountId, cancellationToken);
        var previousTotals = await _repository.GetTotalsAsync(userId, previousFrom, previousTo, accountId, cancellationToken);
        var recurring = await _repository.GetRecurringExpensesAsync(userId, currentFrom, currentTo, accountId, cancellationToken);

        var data = new BehaviorData(
            currentByCategory,
            previousByCategory,
            currentTotals.Expenses,
            previousTotals.Expenses,
            recurring,
            currentTotals.Income,
            currentTotals.Expenses,
            months);

        var insights = BuildInsights(data);
        return new BehaviorReport(
            insights,
            new PeriodInfo(currentFrom, currentTo),
            new PeriodInfo(previousFrom, previousTo),
            DateTimeOffset.UtcNow);
    }

    internal static IReadOnlyList<BehaviorInsight> BuildInsights(BehaviorData data)
    {
        var insights = new List<BehaviorInsight>();

        // 1. Variação por categoria (|variação| >= 10%)
        var currentMap = data.CurrentByCategory
            .GroupBy(c => c.EffectiveCategory)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        var previousMap = data.PreviousByCategory
            .GroupBy(c => c.EffectiveCategory)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        foreach (var (category, currentAmount) in currentMap.OrderByDescending(kv => kv.Value))
        {
            previousMap.TryGetValue(category, out var previousAmount);
            var changePercent = CalculateChangePercent(currentAmount, previousAmount);

            if (Math.Abs(changePercent) >= VariationThresholdPercent && previousAmount > 0)
            {
                var isIncrease = changePercent > 0;
                insights.Add(new BehaviorInsight(
                    isIncrease ? "category_increase" : "category_decrease",
                    category,
                    "spending_change",
                    currentAmount,
                    previousAmount,
                    Math.Round(changePercent, 2),
                    null,
                    $"Seus gastos com {category.ToLowerInvariant()} {(isIncrease ? "aumentaram" : "diminuíram")} {Math.Abs(Math.Round(changePercent, 2))}% nos últimos {data.Months} meses."));
            }
        }

        // 2. Categoria principal do período
        var top = currentMap.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (top.Key is not null && top.Value > 0)
        {
            var topPercentage = data.CurrentExpenses > 0 ? Math.Round(top.Value / data.CurrentExpenses * 100m, 2) : 0m;
            insights.Add(new BehaviorInsight(
                "top_category",
                top.Key,
                "top_category_percentage",
                top.Value,
                null,
                null,
                topPercentage,
                $"{top.Key} é sua maior categoria de gastos ({topPercentage}% das despesas)."));
        }

        // 3. Recorrência
        if (data.CurrentExpenses > 0)
        {
            var recurringRatio = Math.Round(data.RecurringExpenses / data.CurrentExpenses * 100m, 2);
            insights.Add(new BehaviorInsight(
                "recurring_ratio",
                null,
                "recurring_percentage",
                null,
                null,
                null,
                recurringRatio,
                $"{recurringRatio}% das suas despesas são recorrentes."));
        }

        // 4. Saúde do fluxo (receitas vs despesas)
        if (data.CurrentExpenses > 0)
        {
            var ratio = Math.Round(data.Income / data.CurrentExpenses, 2);
            var healthy = ratio >= 1m;
            insights.Add(new BehaviorInsight(
                healthy ? "income_expense_healthy" : "income_expense_risk",
                null,
                "income_expense_ratio",
                data.Income,
                data.CurrentExpenses,
                null,
                ratio,
                healthy
                    ? "Suas receitas cobrem suas despesas (receita/despesa: " + ratio + ")."
                    : "Suas despesas superam as receitas (receita/despesa: " + ratio + ") — atenção ao fluxo de caixa."));
        }

        return insights;
    }

    internal static decimal CalculateChangePercent(decimal current, decimal previous)
    {
        if (previous == 0m)
            return current == 0m ? 0m : 100m;

        return (current - previous) / Math.Abs(previous) * 100m;
    }
}
