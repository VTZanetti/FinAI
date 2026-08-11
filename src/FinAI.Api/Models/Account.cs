using FinAI.Api.Models.Enums;

namespace FinAI.Api.Models;

/// <summary>
/// Conta bancária do usuário.
/// </summary>
public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string Currency { get; set; } = "BRL";
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string? ExternalId { get; set; } // id da conta na fonte externa (ex.: Pluggy) — deduplicação
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
