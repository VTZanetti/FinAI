using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;

namespace FinAI.Api.Services.OpenFinance;

public sealed record OpenFinanceStatus(
    OpenFinanceSyncInfo? LastSync,
    int ConnectionsCount);

public sealed record OpenFinanceSyncInfo(
    Guid Id,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int AccountsImported,
    int TransactionsImported,
    int TransactionsSkipped,
    string? Error);

public interface IOpenFinanceStatusService
{
    Task<Result<OpenFinanceStatus>> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class OpenFinanceStatusService : IOpenFinanceStatusService
{
    private readonly IOpenFinanceRepository _repository;

    public OpenFinanceStatusService(IOpenFinanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OpenFinanceStatus>> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var lastSync = await _repository.GetLastSyncAsync(userId, cancellationToken);
        var connections = await _repository.ListConnectionsAsync(userId, cancellationToken);

        OpenFinanceSyncInfo? info = null;
        if (lastSync is not null)
        {
            info = new OpenFinanceSyncInfo(
                lastSync.Id,
                lastSync.Status.ToString(),
                lastSync.StartedAt,
                lastSync.FinishedAt,
                lastSync.AccountsImported,
                lastSync.TransactionsImported,
                lastSync.TransactionsSkipped,
                lastSync.Error);
        }

        return Result.Success(new OpenFinanceStatus(info, connections.Count));
    }
}