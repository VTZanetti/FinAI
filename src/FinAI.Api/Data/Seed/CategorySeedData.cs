using FinAI.Api.Models;

namespace FinAI.Api.Data.Seed;

/// <summary>
/// Categorias do sistema (IsSystem = true, UserId = Guid.Empty) — seed aplicado na migração inicial.
/// </summary>
public static class CategorySeedData
{
    public static readonly IReadOnlyList<(string Name, string? Subcategory)> Items =
    [
        ("Food", "Restaurant"),
        ("Food", "Groceries"),
        ("Transportation", "Ride Sharing"),
        ("Transportation", "Fuel"),
        ("Transportation", "Public Transit"),
        ("Housing", "Rent"),
        ("Housing", "Mortgage"),
        ("Utilities", "Electricity"),
        ("Utilities", "Water"),
        ("Utilities", "Internet"),
        ("Health", "Pharmacy"),
        ("Health", "Medical"),
        ("Entertainment", "Streaming"),
        ("Entertainment", "Games"),
        ("Education", "Courses"),
        ("Travel", "Flights"),
        ("Shopping", "Online"),
        ("Shopping", "Retail"),
        ("Income", "Salary"),
        ("Income", "Freelance"),
        ("Other", null)
    ];

    public static IEnumerable<Category> CreateSystemCategories()
        => Items.Select((item, index) => new Category
        {
            Id = SystemCategoryIds[index],
            UserId = Guid.Empty,
            Name = item.Name,
            Subcategory = item.Subcategory,
            IsSystem = true
        });

    /// <summary>
    /// IDs fixos e estáveis das categorias do sistema (necessários para o seed idempotente).
    /// </summary>
    public static readonly IReadOnlyList<Guid> SystemCategoryIds =
    [
        Guid.Parse("11111111-1111-1111-1111-111111111101"),
        Guid.Parse("11111111-1111-1111-1111-111111111102"),
        Guid.Parse("11111111-1111-1111-1111-111111111103"),
        Guid.Parse("11111111-1111-1111-1111-111111111104"),
        Guid.Parse("11111111-1111-1111-1111-111111111105"),
        Guid.Parse("11111111-1111-1111-1111-111111111106"),
        Guid.Parse("11111111-1111-1111-1111-111111111107"),
        Guid.Parse("11111111-1111-1111-1111-111111111108"),
        Guid.Parse("11111111-1111-1111-1111-111111111109"),
        Guid.Parse("11111111-1111-1111-1111-11111111110a"),
        Guid.Parse("11111111-1111-1111-1111-11111111110b"),
        Guid.Parse("11111111-1111-1111-1111-11111111110c"),
        Guid.Parse("11111111-1111-1111-1111-11111111110d"),
        Guid.Parse("11111111-1111-1111-1111-11111111110e"),
        Guid.Parse("11111111-1111-1111-1111-11111111110f"),
        Guid.Parse("11111111-1111-1111-1111-111111111110"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("11111111-1111-1111-1111-111111111112"),
        Guid.Parse("11111111-1111-1111-1111-111111111113"),
        Guid.Parse("11111111-1111-1111-1111-111111111114"),
        Guid.Parse("11111111-1111-1111-1111-111111111115")
    ];
}
