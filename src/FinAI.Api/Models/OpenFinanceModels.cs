namespace FinAI.Api.Models;

public enum SyncStatus
{
    Running = 1,
    Success = 2,
    Failed = 3
}

/// <summary>
/// Registro de sincronização Open Finance (append de histórico).
/// </summary>
public class OpenFinanceSync
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ConnectionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public SyncStatus Status { get; set; }
    public int AccountsImported { get; set; }
    public int TransactionsImported { get; set; }
    public int TransactionsSkipped { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Conexão bancária do usuário (Modo B — Connect Widget). itemId vinculado ao UserId.
/// </summary>
public class UserBankConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
}