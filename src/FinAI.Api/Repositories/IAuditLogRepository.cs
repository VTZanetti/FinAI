using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface IAuditLogRepository
{
    /// <summary>Append-only: apenas insere.</summary>
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);
}

public sealed record AuditLogFilter(
    Guid? UserId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);
