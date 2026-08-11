using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class OpenFinanceSyncConfiguration : IEntityTypeConfiguration<OpenFinanceSync>
{
    public void Configure(EntityTypeBuilder<OpenFinanceSync> builder)
    {
        builder.ToTable("open_finance_syncs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(s => s.Error)
            .HasMaxLength(500);

        builder.HasIndex(s => new { s.UserId, s.StartedAt });
    }
}

public class UserBankConnectionConfiguration : IEntityTypeConfiguration<UserBankConnection>
{
    public void Configure(EntityTypeBuilder<UserBankConnection> builder)
    {
        builder.ToTable("user_bank_connections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ItemId)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.InstitutionName)
            .HasMaxLength(120);

        // Um itemId por usuário (vínculo único)
        builder.HasIndex(c => new { c.UserId, c.ItemId }).IsUnique();
    }
}