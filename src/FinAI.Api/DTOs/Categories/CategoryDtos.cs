using FinAI.Api.Models;

namespace FinAI.Api.DTOs.Categories;

public sealed record CategoryResponse(Guid Id, string Name, string? Subcategory, bool IsSystem);

public static class CategoryMappings
{
    public static CategoryResponse ToResponse(this Category category)
        => new(category.Id, category.Name, category.Subcategory, category.IsSystem);
}
