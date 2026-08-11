using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;

namespace FinAI.Api.Services.Transactions;

public interface ITransactionService
{
    Task<ServiceResult<Transaction>> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<Transaction>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<Transaction>>> ListAsync(Guid userId, TransactionFilter filter, CancellationToken cancellationToken = default);
    Task<ServiceResult<Transaction>> UpdateAsync(Guid userId, Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
