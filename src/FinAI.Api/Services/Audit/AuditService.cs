using System.Text.Json;
using FinAI.Api.Repositories;
using FinAI.Api.Security;

namespace FinAI.Api.Services.Audit;

public interface IAuditService
{
    Task RecordAsync(string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Models.AuditLog>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Serviço de auditoria — registra eventos append-only com mascaramento de valores sensíveis.
/// </summary>
public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IAuditLogRepository _logs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        IAuditLogRepository logs,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _logs = logs;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RecordAsync(string action, string entityType, Guid? entityId, object? metadata = null, CancellationToken cancellationToken = default)
    {
        var log = new Models.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId ?? Guid.Empty,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(AuditDataSanitizer.Sanitize(metadata), JsonOptions),
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            TraceId = _httpContextAccessor.HttpContext?.TraceIdentifier,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await _logs.AddAsync(log, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Models.AuditLog>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
        => _logs.QueryAsync(filter, cancellationToken);
}

/// <summary>
/// Mascara valores sensíveis (Amount, Balance, Password, Token, etc.) no metadata de auditoria.
/// </summary>
public static class AuditDataSanitizer
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "amount", "balance", "initialbalance", "currentbalance", "limitamount",
        "password", "token", "refreshtoken", "accesstoken", "email", "secret", "apikey"
    };

    public static object Sanitize(object data)
    {
        if (data is null)
            return new { };

        // Serializa → dicionário → mascarar → objeto
        var json = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(json);

        var dict = new Dictionary<string, object?>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = SensitiveKeys.Contains(prop.Name)
                ? "***"
                : prop.Value.ValueKind == JsonValueKind.Object ? SanitizeNested(prop.Value) : GetValue(prop.Value);
        }

        return dict;
    }

    private static object SanitizeNested(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = SensitiveKeys.Contains(prop.Name)
                ? "***"
                : prop.Value.ValueKind == JsonValueKind.Object ? SanitizeNested(prop.Value) : GetValue(prop.Value);
        }
        return dict;
    }

    private static object? GetValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetDecimal(out var d) ? d : element.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(GetValue).ToArray(),
        _ => element.GetRawText()
    };
}
