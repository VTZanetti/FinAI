using FinAI.Api.Data;
using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private const int MaxPageSize = 100;

    private readonly FinAiDbContext _db;

    public TransactionRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _db.Transactions
            .Include(t => t.Category)
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

    public async Task<TransactionQueryResult> QueryAsync(
        Guid userId,
        TransactionFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Where(t => t.UserId == userId);

        if (filter.AccountId.HasValue)
            query = query.Where(t => t.AccountId == filter.AccountId);

        if (filter.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == filter.CategoryId);

        if (filter.Type.HasValue)
            query = query.Where(t => t.Type == filter.Type);

        if (filter.From.HasValue)
            query = query.Where(t => t.Date >= filter.From);

        if (filter.To.HasValue)
            query = query.Where(t => t.Date <= filter.To);

        if (filter.MinAmount.HasValue)
            query = query.Where(t => t.Amount >= filter.MinAmount);

        if (filter.MaxAmount.HasValue)
            query = query.Where(t => t.Amount <= filter.MaxAmount);

        if (filter.IsRecurring.HasValue)
            query = query.Where(t => t.IsRecurring == filter.IsRecurring);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(t => t.Description.ToLower().Contains(term.ToLower()));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var sortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "date" : filter.SortBy.ToLowerInvariant();
        var sortOrder = string.Equals(filter.SortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

        query = sortBy switch
        {
            "amount" => sortOrder == "asc" ? query.OrderBy(t => t.Amount) : query.OrderByDescending(t => t.Amount),
            "createdat" => sortOrder == "asc" ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt),
            _ => sortOrder == "asc" ? query.OrderBy(t => t.Date) : query.OrderByDescending(t => t.Date)
        };

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        return new TransactionQueryResult(items, totalItems, page, pageSize, totalPages);
    }

    public Task<bool> ExistsByExternalIdAsync(Guid userId, string externalId, CancellationToken cancellationToken = default)
        => _db.Transactions.AnyAsync(t => t.UserId == userId && t.ExternalId == externalId, cancellationToken);

    public async Task<decimal> SumExpensesByCategoryAsync(Guid userId, Guid categoryId, int year, int month, CancellationToken cancellationToken = default)
        => await _db.Transactions
            .Where(t => t.UserId == userId
                        && t.CategoryId == categoryId
                        && t.Type == TransactionType.Expense
                        && t.Date.Year == year
                        && t.Date.Month == month)
            .Select(t => (decimal?)t.Amount)
            .SumAsync(cancellationToken) ?? 0m;

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => await _db.Transactions.AddAsync(transaction, cancellationToken);

    public void Update(Transaction transaction) => _db.Transactions.Update(transaction);

    public void Delete(Transaction transaction) => _db.Transactions.Remove(transaction);
}
