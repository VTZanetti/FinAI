using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.InitialBalance)
            .HasPrecision(18, 2);

        builder.Property(a => a.CurrentBalance)
            .HasPrecision(18, 2);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(a => new { a.UserId, a.Name });

        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
