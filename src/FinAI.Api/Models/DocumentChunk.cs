using Pgvector;

namespace FinAI.Api.Models;

/// <summary>
/// Chunk de documento com embedding vetorial (pgvector) — FR-10.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
    public int Tokens { get; set; }

    public FinancialDocument? Document { get; set; }
}
