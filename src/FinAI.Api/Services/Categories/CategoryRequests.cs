namespace FinAI.Api.Services.Categories;

public sealed record CreateCategoryRequest(string Name, string? Subcategory = null);

public sealed record UpdateCategoryRequest(string Name, string? Subcategory = null);
