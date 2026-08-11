using FinAI.Api.Common;
using FinAI.Api.Models;

namespace FinAI.Api.Services.Budgets;

public interface IBudgetService
{
    Task<Result<Budget>> CreateAsync(Guid userId, CreateBudgetRequest request, CancellationToken cancellationToken = default);
    Task<Result<Budget>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Budget>>> ListAsync(Guid userId, int? month, int? year, CancellationToken cancellationToken = default);
    Task<Result<Budget>> UpdateAsync(Guid userId, Guid id, UpdateBudgetRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<decimal> GetSpentAmountAsync(Guid userId, Budget budget, CancellationToken cancellationToken = default);
}
