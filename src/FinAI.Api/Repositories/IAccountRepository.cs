using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Account?> FindByExternalIdAsync(Guid userId, string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountTransactionsAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    void Update(Account account);
    void Delete(Account account);
}
