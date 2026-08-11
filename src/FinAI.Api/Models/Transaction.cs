using FinAI.Api.Models.Enums;

namespace FinAI.Api.Models;

/// <summary>
/// Transação financeira do usuário. Valor positivo = receita, negativo = despesa.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public TransactionType Type { get; set; }
    public bool IsRecurring { get; set; }
    public string? ExternalId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public Account? Account { get; set; }
    public Category? Category { get; set; }
}
