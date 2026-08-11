using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinAI.Api.Models;

namespace FinAI.Api.Data.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.LimitAmount)
            .HasPrecision(18, 2);

        builder.Property(b => b.Month)
            .HasComment("Mês do orçamento (1..12)");

        builder.Property(b => b.Year)
            .HasComment("Ano do orçamento");

        // Um orçamento por (usuário, categoria, mês, ano)
        builder.HasIndex(b => new { b.UserId, b.CategoryId, b.Year, b.Month })
            .IsUnique();
    }
}
