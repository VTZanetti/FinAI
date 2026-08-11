using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> ListForUserAsync(Guid userId, string? search = null, CancellationToken cancellationToken = default);
    Task<int> CountTransactionsAsync(Guid categoryId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid userId, string name, string? subcategory, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    void Update(Category category);
    void Delete(Category category);
}
