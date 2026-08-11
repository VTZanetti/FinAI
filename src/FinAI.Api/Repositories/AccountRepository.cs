using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly FinAiDbContext _db;

    public AccountRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

    public Task<Account?> FindByExternalIdAsync(Guid userId, string externalId, CancellationToken cancellationToken = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.ExternalId == externalId, cancellationToken);

    public async Task<IReadOnlyList<Account>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _db.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _db.Accounts.AnyAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

    public Task<int> CountTransactionsAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default)
        => _db.Transactions.CountAsync(t => t.AccountId == accountId && t.UserId == userId, cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
        => await _db.Accounts.AddAsync(account, cancellationToken);

    public void Update(Account account) => _db.Accounts.Update(account);

    public void Delete(Account account) => _db.Accounts.Remove(account);
}
