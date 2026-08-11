using FinAI.Api.Models;

namespace FinAI.Api.Repositories;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Budget>> ListByPeriodAsync(Guid userId, int? month, int? year, CancellationToken cancellationToken = default);
    Task<Budget?> GetByCategoryPeriodAsync(Guid userId, Guid categoryId, int year, int month, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCategoryPeriodAsync(Guid userId, Guid categoryId, int year, int month, CancellationToken cancellationToken = default);
    Task AddAsync(Budget budget, CancellationToken cancellationToken = default);
    void Update(Budget budget);
    void Delete(Budget budget);
}
