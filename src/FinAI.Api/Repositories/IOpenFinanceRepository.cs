using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface IOpenFinanceRepository
{
    Task<OpenFinanceSync?> GetLastSyncAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddSyncAsync(OpenFinanceSync sync, CancellationToken cancellationToken = default);
    void UpdateSync(OpenFinanceSync sync);
    Task<UserBankConnection?> GetConnectionAsync(Guid userId, string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserBankConnection>> ListConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddConnectionAsync(UserBankConnection connection, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListUsersWithItemAsync(string itemId, CancellationToken cancellationToken = default);
}