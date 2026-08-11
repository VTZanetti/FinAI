using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class OpenFinanceRepository : IOpenFinanceRepository
{
    private readonly FinAiDbContext _db;

    public OpenFinanceRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<OpenFinanceSync?> GetLastSyncAsync(Guid userId, CancellationToken cancellationToken = default)
        => _db.OpenFinanceSyncs
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddSyncAsync(OpenFinanceSync sync, CancellationToken cancellationToken = default)
        => await _db.OpenFinanceSyncs.AddAsync(sync, cancellationToken);

    public void UpdateSync(OpenFinanceSync sync) => _db.OpenFinanceSyncs.Update(sync);

    public Task<UserBankConnection?> GetConnectionAsync(Guid userId, string itemId, CancellationToken cancellationToken = default)
        => _db.UserBankConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ItemId == itemId, cancellationToken);

    public async Task<IReadOnlyList<UserBankConnection>> ListConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _db.UserBankConnections
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddConnectionAsync(UserBankConnection connection, CancellationToken cancellationToken = default)
        => await _db.UserBankConnections.AddAsync(connection, cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListUsersWithItemAsync(string itemId, CancellationToken cancellationToken = default)
        => await _db.UserBankConnections
            .AsNoTracking()
            .Where(c => c.ItemId == itemId)
            .Select(c => c.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
}