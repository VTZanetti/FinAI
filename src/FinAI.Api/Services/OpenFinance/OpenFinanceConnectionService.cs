using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.OpenFinance.Models;

namespace FinAI.Api.Services.OpenFinance;

public sealed record ConnectTokenResult(string AccessToken, int ExpiresAt);

public sealed record ConnectionResult(Guid Id, string ItemId, string? InstitutionName, DateTimeOffset CreatedAt);

public interface IOpenFinanceConnectionService
{
    Task<Result<ConnectTokenResult>> CreateConnectTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ConnectionResult>> LinkConnectionAsync(Guid userId, string itemId, string? institutionName = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ConnectionResult>>> ListConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modo B (Connect Widget): connect-token por usuário + vínculo itemId ↔ UserId.
/// </summary>
public class OpenFinanceConnectionService : IOpenFinanceConnectionService
{
    private readonly IPluggyClient _pluggy;
    private readonly IOpenFinanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public OpenFinanceConnectionService(IPluggyClient pluggy, IOpenFinanceRepository repository, IUnitOfWork unitOfWork)
    {
        _pluggy = pluggy;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConnectTokenResult>> CreateConnectTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var token = await _pluggy.CreateConnectTokenAsync(userId.ToString(), cancellationToken);
        return Result.Success(new ConnectTokenResult(token.AccessToken, token.ExpiresAt));
    }

    public async Task<Result<ConnectionResult>> LinkConnectionAsync(Guid userId, string itemId, string? institutionName = null, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetConnectionAsync(userId, itemId, cancellationToken);
        if (existing is not null)
            return Result.Success(new ConnectionResult(existing.Id, existing.ItemId, existing.InstitutionName, existing.CreatedAt));

        var connection = new UserBankConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = itemId,
            InstitutionName = institutionName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddConnectionAsync(connection, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConnectionResult(connection.Id, connection.ItemId, connection.InstitutionName, connection.CreatedAt));
    }

    public async Task<Result<IReadOnlyList<ConnectionResult>>> ListConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connections = await _repository.ListConnectionsAsync(userId, cancellationToken);
        return Result.Success<IReadOnlyList<ConnectionResult>>(
            connections.Select(c => new ConnectionResult(c.Id, c.ItemId, c.InstitutionName, c.CreatedAt)).ToList());
    }
}