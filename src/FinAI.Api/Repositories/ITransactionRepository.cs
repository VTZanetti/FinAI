using FinAI.Api.Models;
using FinAI.Api.Models.Enums;

namespace FinAI.Api.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<TransactionQueryResult> QueryAsync(
        Guid userId,
        TransactionFilter filter,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByExternalIdAsync(Guid userId, string externalId, CancellationToken cancellationToken = default);
    Task<decimal> SumExpensesByCategoryAsync(Guid userId, Guid categoryId, int year, int month, CancellationToken cancellationToken = default);
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    void Update(Transaction transaction);
    void Delete(Transaction transaction);
}

public sealed record TransactionFilter(
    Guid? AccountId = null,
    Guid? CategoryId = null,
    TransactionType? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? Search = null,
    bool? IsRecurring = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortOrder = null);

public sealed record TransactionQueryResult(
    IReadOnlyList<Transaction> Items,
    int TotalItems,
    int Page,
    int PageSize,
    int TotalPages);
