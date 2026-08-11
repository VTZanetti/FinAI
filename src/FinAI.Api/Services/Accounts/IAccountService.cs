using FinAI.Api.Common;
using FinAI.Api.Models;

namespace FinAI.Api.Services.Accounts;

public interface IAccountService
{
    Task<Result<Account>> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken = default);
    Task<Result<Account>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Account>>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<Account>> UpdateAsync(Guid userId, Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
