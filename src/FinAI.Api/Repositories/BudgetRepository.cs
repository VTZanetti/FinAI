using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class BudgetRepository : IBudgetRepository
{
    private readonly FinAiDbContext _db;

    public BudgetRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<Budget?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Budget>> ListByPeriodAsync(Guid userId, int? month, int? year, CancellationToken cancellationToken = default)
    {
        var query = _db.Budgets
            .AsNoTracking()
            .Where(b => b.UserId == userId);

        if (month.HasValue)
            query = query.Where(b => b.Month == month);

        if (year.HasValue)
            query = query.Where(b => b.Year == year);

        return await query
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ToListAsync(cancellationToken);
    }

    public Task<Budget?> GetByCategoryPeriodAsync(Guid userId, Guid categoryId, int year, int month, CancellationToken cancellationToken = default)
        => _db.Budgets.FirstOrDefaultAsync(
            b => b.UserId == userId && b.CategoryId == categoryId && b.Year == year && b.Month == month,
            cancellationToken);

    public Task<bool> ExistsByCategoryPeriodAsync(Guid userId, Guid categoryId, int year, int month, CancellationToken cancellationToken = default)
        => _db.Budgets.AnyAsync(
            b => b.UserId == userId && b.CategoryId == categoryId && b.Year == year && b.Month == month,
            cancellationToken);

    public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default)
        => await _db.Budgets.AddAsync(budget, cancellationToken);

    public void Update(Budget budget) => _db.Budgets.Update(budget);

    public void Delete(Budget budget) => _db.Budgets.Remove(budget);
}
