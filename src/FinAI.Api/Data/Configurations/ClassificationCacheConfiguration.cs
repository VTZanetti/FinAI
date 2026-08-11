using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class ClassificationCacheConfiguration : IEntityTypeConfiguration<ClassificationCache>
{
    public void Configure(EntityTypeBuilder<ClassificationCache> builder)
    {
        builder.ToTable("classification_cache");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.NormalizedDescription)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.AmountBucket)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(c => c.Confidence)
            .HasPrecision(5, 4);

        builder.HasIndex(c => new { c.UserId, c.NormalizedDescription, c.AmountBucket });
    }
}
