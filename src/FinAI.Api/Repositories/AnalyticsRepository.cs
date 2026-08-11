using FinAI.Api.Data;
using FinAI.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly FinAiDbContext _db;

    public AnalyticsRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public async Task<TotalsResult> GetTotalsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= from && t.Date <= to);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId);

        var income = await query.Where(t => t.Type == TransactionType.Income).SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
        var expenses = await query.Where(t => t.Type == TransactionType.Expense).SumAsync(t => (decimal?)Math.Abs(t.Amount), cancellationToken) ?? 0m;

        return new TotalsResult(income, expenses, income - expenses);
    }

    public async Task<IReadOnlyList<CategoryAggregate>> GetExpensesByCategoryAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.Type == TransactionType.Expense
                        && t.Date >= from
                        && t.Date <= to);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId);

        // Agregação no banco por CategoryId; nomes resolvidos após a query (NFR-02)
        var grouped = await query
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(t => Math.Abs(t.Amount)) })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(cancellationToken);

        var categoryIds = grouped.Where(g => g.CategoryId.HasValue).Select(g => g.CategoryId!.Value).Distinct().ToList();
        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return grouped
            .Select(g => g.CategoryId.HasValue && categories.TryGetValue(g.CategoryId.Value, out var c)
                ? new CategoryAggregate(c.Name, c.Subcategory, g.Amount)
                : new CategoryAggregate(null, null, g.Amount))
            .ToList();
    }

    public async Task<decimal> GetRecurringExpensesAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.Type == TransactionType.Expense
                        && t.IsRecurring
                        && t.Date >= from
                        && t.Date <= to);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId);

        return await query.SumAsync(t => (decimal?)Math.Abs(t.Amount), cancellationToken) ?? 0m;
    }

    public async Task<IReadOnlyList<MonthlyAggregate>> GetMonthlyTotalsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken cancellationToken = default)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= from && t.Date <= to);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId);

        // Somas separadas por tipo sem Math.Abs (não traduzido dentro de GroupBy)
        var rows = await query
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => (decimal?)t.Amount) ?? 0m,
                Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => (decimal?)-t.Amount) ?? 0m
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new MonthlyAggregate(r.Year, r.Month, r.Income, r.Expenses))
            .ToList();
    }
}
