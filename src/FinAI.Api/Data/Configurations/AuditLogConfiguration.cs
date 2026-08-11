using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.EntityType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.TraceId)
            .HasMaxLength(64);

        builder.HasIndex(a => new { a.UserId, a.OccurredAt });
        builder.HasIndex(a => a.Action);
    }
}
