using FinAI.Api.Data;
using FinAI.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Api.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly FinAiDbContext _db;

    public CategoryRepository(FinAiDbContext db)
    {
        _db = db;
    }

    public Task<Category?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && (c.UserId == userId || c.IsSystem), cancellationToken);

    public async Task<IReadOnlyList<Category>> ListForUserAsync(Guid userId, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId || c.IsSystem);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Subcategory != null && c.Subcategory.Contains(term)));
        }

        return await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Subcategory)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountTransactionsAsync(Guid categoryId, Guid userId, CancellationToken cancellationToken = default)
        => _db.Transactions.CountAsync(t => t.CategoryId == categoryId && t.UserId == userId, cancellationToken);

    public Task<bool> ExistsByNameAsync(Guid userId, string name, string? subcategory, CancellationToken cancellationToken = default)
        => _db.Categories.AnyAsync(c => c.UserId == userId && c.Name == name && c.Subcategory == subcategory, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        => await _db.Categories.AddAsync(category, cancellationToken);

    public void Update(Category category) => _db.Categories.Update(category);

    public void Delete(Category category) => _db.Categories.Remove(category);
}
