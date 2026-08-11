using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;

namespace FinAI.Api.Services.Categories;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Category>> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _categories.ExistsByNameAsync(userId, name, request.Subcategory?.Trim(), cancellationToken))
            return Result.Failure<Category>(ErrorCode.Conflict, "Category with the same name already exists");

        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Subcategory = string.IsNullOrWhiteSpace(request.Subcategory) ? null : request.Subcategory.Trim(),
            IsSystem = false
        };

        await _categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category);
    }

    public async Task<Result<Category>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, userId, cancellationToken);
        return category is null
            ? Result.Failure<Category>(ErrorCode.NotFound, "Category not found")
            : Result.Success(category);
    }

    public async Task<Result<IReadOnlyList<Category>>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListForUserAsync(userId, search, cancellationToken);
        return Result.Success(categories);
    }

    public async Task<Result<Category>> UpdateAsync(Guid userId, Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, userId, cancellationToken);
        if (category is null)
            return Result.Failure<Category>(ErrorCode.NotFound, "Category not found");

        if (category.IsSystem)
            return Result.Failure<Category>(ErrorCode.Forbidden, "System categories are read-only");

        category.Name = request.Name.Trim();
        category.Subcategory = string.IsNullOrWhiteSpace(request.Subcategory) ? null : request.Subcategory.Trim();

        _categories.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, userId, cancellationToken);
        if (category is null)
            return Result.Failure(ErrorCode.NotFound, "Category not found");

        if (category.IsSystem)
            return Result.Failure(ErrorCode.Forbidden, "System categories cannot be deleted");

        var transactionCount = await _categories.CountTransactionsAsync(category.Id, userId, cancellationToken);
        if (transactionCount > 0)
            return Result.Failure(ErrorCode.Conflict, "Cannot delete category with linked transactions");

        _categories.Delete(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
