namespace FinAI.Api.Models;

/// <summary>
/// Registro de auditoria — append-only (nunca UPDATE/DELETE).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? MetadataJson { get; set; }
    public string? IpAddress { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
