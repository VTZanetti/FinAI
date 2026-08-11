using FinAI.Api.Repositories;
using FinAI.Api.Services.Analytics.Models;

namespace FinAI.Api.Services.Analytics;

public interface ISpendingAnalyzer
{
    Task<SpendingSummary> AnalyzeAsync(AnalyticsFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Totais, composição por categoria e recorrência de despesas no período.
/// </summary>
public class SpendingAnalyzer : ISpendingAnalyzer
{
    private readonly IAnalyticsRepository _repository;

    public SpendingAnalyzer(IAnalyticsRepository repository)
    {
        _repository = repository;
    }

    public async Task<SpendingSummary> AnalyzeAsync(AnalyticsFilter filter, CancellationToken cancellationToken = default)
    {
        var totals = await _repository.GetTotalsAsync(filter.UserId, filter.From, filter.To, filter.AccountId, cancellationToken);
        var byCategory = await _repository.GetExpensesByCategoryAsync(filter.UserId, filter.From, filter.To, filter.AccountId, cancellationToken);
        var recurring = await _repository.GetRecurringExpensesAsync(filter.UserId, filter.From, filter.To, filter.AccountId, cancellationToken);

        var totalExpenses = totals.Expenses;

        var categories = byCategory
            .Select(c => new CategorySpending(
                c.EffectiveCategory,
                c.Subcategory,
                c.Amount,
                totalExpenses > 0 ? Math.Round(c.Amount / totalExpenses * 100m, 2) : 0m))
            .ToList();

        // Garante que existe "Uncategorized" mesmo sem transações sem categoria
        if (totalExpenses > 0 && !categories.Any(c => c.Category == "Uncategorized"))
            categories.Add(new CategorySpending("Uncategorized", null, 0m, 0m));

        var recurringPercentage = totalExpenses > 0 ? Math.Round(recurring / totalExpenses * 100m, 2) : 0m;

        return new SpendingSummary(
            new PeriodInfo(filter.From, filter.To),
            totals,
            categories,
            new RecurringInfo(Math.Round(recurring, 2), recurringPercentage),
            DateTimeOffset.UtcNow);
    }
}
