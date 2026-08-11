using FinAI.Api.Data.Configurations;
using FinAI.Api.Data.Seed;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Data;

/// <summary>
/// DbContext do FinAI — tabelas em snake_case, valores monetários decimal(18,2).
/// </summary>
public class FinAiDbContext : DbContext
{
    public FinAiDbContext(DbContextOptions<FinAiDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinAiDbContext).Assembly);

        // Convenção: nomes de tabelas e colunas em snake_case
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName() ?? entity.DisplayName()));

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }
        }

        // Seed das categorias do sistema
        modelBuilder.Entity<Category>().HasData(CategorySeedData.CreateSystemCategories());

        base.OnModelCreating(modelBuilder);
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && input[i - 1] != '_' && !char.IsUpper(input[i - 1]))
                    sb.Append('_');
                else if (i > 1 && char.IsUpper(input[i - 1]) && i + 1 < input.Length && char.IsLower(input[i + 1]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
