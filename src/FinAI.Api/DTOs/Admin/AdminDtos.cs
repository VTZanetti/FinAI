using FinAI.Api.Models;

namespace FinAI.Api.DTOs.Admin;

public sealed record AuditLogResponse(
    Guid Id,
    Guid UserId,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? MetadataJson,
    string? IpAddress,
    string? TraceId,
    DateTimeOffset OccurredAt);

public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Roles);

public static class AdminMappings
{
    public static AuditLogResponse ToResponse(this AuditLog log)
        => new(log.Id, log.UserId, log.Action, log.EntityType, log.EntityId, log.MetadataJson, log.IpAddress, log.TraceId, log.OccurredAt);
}
