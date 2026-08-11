using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Description)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(t => t.ExternalId)
            .HasMaxLength(120);

        // Deduplicação por (UserId, ExternalId) — índice único filtrado (ExternalId não nulo)
        builder.HasIndex(t => new { t.UserId, t.ExternalId })
            .IsUnique()
            .HasFilter("\"external_id\" IS NOT NULL");

        // Filtros frequentes: período por usuário
        builder.HasIndex(t => new { t.UserId, t.Date });
        builder.HasIndex(t => new { t.UserId, t.CategoryId, t.Date });
        builder.HasIndex(t => new { t.UserId, t.AccountId, t.Date });
    }
}
