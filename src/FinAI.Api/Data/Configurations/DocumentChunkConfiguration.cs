using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .HasColumnType("text")
            .IsRequired();

        // Embedding vetorial (pgvector) — 768 dimensões (nomic-embed-text)
        builder.Property(c => c.Embedding)
            .HasColumnType("vector(768)");

        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex });
    }
}
