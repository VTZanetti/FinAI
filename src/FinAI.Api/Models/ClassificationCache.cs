namespace FinAI.Api.Models;

/// <summary>
/// Cache de classificações aprendidas (exemplos bem-sucedidos do LLM) — ADR-005, etapa 2.
/// </summary>
public class ClassificationCache
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NormalizedDescription { get; set; } = string.Empty;
    public string AmountBucket { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public decimal Confidence { get; set; }
    public int HitCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
}
