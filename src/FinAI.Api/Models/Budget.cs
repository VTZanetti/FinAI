namespace FinAI.Api.Models;

/// <summary>
/// Orçamento por categoria para um mês/ano específico.
/// </summary>
public class Budget
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal LimitAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
