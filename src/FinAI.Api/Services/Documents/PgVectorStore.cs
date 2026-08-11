using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace FinAI.Api.Services.Documents;

public sealed record SemanticSearchHit(Guid ChunkId, Guid DocumentId, string FileName, string Content, float Score);

/// <summary>
/// Armazenamento vetorial no PostgreSQL (pgvector) — upsert de chunks + busca semântica por cosseno.
/// </summary>
public interface IVectorStore
{
    Task UpsertChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
    Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SemanticSearchHit>> SearchAsync(Guid userId, Vector embedding, int topK, double minScore, CancellationToken cancellationToken = default);
}

public class PgVectorStore : IVectorStore
{
    private readonly FinAiDbContext _db;

    public PgVectorStore(FinAiDbContext db)
    {
        _db = db;
    }

    public async Task UpsertChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
    {
        var existing = await _db.DocumentChunks
            .FirstOrDefaultAsync(c => c.DocumentId == chunk.DocumentId && c.ChunkIndex == chunk.ChunkIndex, cancellationToken);

        if (existing is not null)
        {
            existing.Content = chunk.Content;
            existing.Embedding = chunk.Embedding;
            existing.Tokens = chunk.Tokens;
            _db.DocumentChunks.Update(existing);
        }
        else
        {
            await _db.DocumentChunks.AddAsync(chunk, cancellationToken);
        }
    }

    public async Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var chunks = await _db.DocumentChunks.Where(c => c.DocumentId == documentId).ToListAsync(cancellationToken);
        _db.DocumentChunks.RemoveRange(chunks);
    }

    public async Task<IReadOnlyList<SemanticSearchHit>> SearchAsync(Guid userId, Vector embedding, int topK, double minScore, CancellationToken cancellationToken = default)
    {
        // Busca semântica com distância cosseno (<=>) — filtro obrigatório por usuário (sem vazamento entre usuários)
        var query = $"""
            SELECT c.id AS "ChunkId", c.document_id AS "DocumentId", d.file_name AS "FileName",
                   c.content AS "Content", (1 - (c.embedding <=> @embedding)) AS "Score"
            FROM document_chunks c
            INNER JOIN documents d ON d.id = c.document_id
            WHERE d.user_id = @userId AND c.embedding IS NOT NULL
            ORDER BY c.embedding <=> @embedding
            LIMIT @topK
            """;

        var hits = await _db.Database.SqlQueryRaw<SemanticSearchRow>(query,
            new Npgsql.NpgsqlParameter("embedding", embedding),
            new Npgsql.NpgsqlParameter("userId", userId),
            new Npgsql.NpgsqlParameter("topK", topK)).ToListAsync(cancellationToken);

        return hits
            .Select(h => new SemanticSearchHit(h.ChunkId, h.DocumentId, h.FileName, h.Content, h.Score))
            .Where(h => h.Score >= minScore)
            .ToList();
    }

    private sealed class SemanticSearchRow
    {
        public Guid ChunkId { get; set; }
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float Score { get; set; }
    }
}
