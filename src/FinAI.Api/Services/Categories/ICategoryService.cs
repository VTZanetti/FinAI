using FinAI.Api.Common;
using FinAI.Api.Models;

namespace FinAI.Api.Services.Categories;

public interface ICategoryService
{
    Task<Result<Category>> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<Category>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Category>>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default);
    Task<Result<Category>> UpdateAsync(Guid userId, Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
