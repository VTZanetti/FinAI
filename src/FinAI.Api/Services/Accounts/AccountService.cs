using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services.Audit;

namespace FinAI.Api.Services.Accounts;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;

    public AccountService(IAccountRepository accounts, IUnitOfWork unitOfWork, IAuditService audit)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<Result<Account>> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = request.Currency.ToUpperInvariant(),
            InitialBalance = request.InitialBalance,
            CurrentBalance = request.InitialBalance, // regra: saldo inicial = saldo atual na criação
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _accounts.AddAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync("account.create", "Account", account.Id, new { name = account.Name }, cancellationToken);

        return Result.Success(account);
    }

    public async Task<Result<Account>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, userId, cancellationToken);
        return account is null
            ? Result.Failure<Account>(ErrorCode.NotFound, "Account not found")
            : Result.Success(account);
    }

    public async Task<Result<IReadOnlyList<Account>>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.ListByUserAsync(userId, cancellationToken);
        return Result.Success(accounts);
    }

    public async Task<Result<Account>> UpdateAsync(Guid userId, Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, userId, cancellationToken);
        if (account is null)
            return Result.Failure<Account>(ErrorCode.NotFound, "Account not found");

        // initialBalance não é editável — apenas name/type/currency
        account.Name = request.Name.Trim();
        account.Type = request.Type;
        account.Currency = request.Currency.ToUpperInvariant();
        account.UpdatedAt = DateTimeOffset.UtcNow;

        _accounts.Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync("account.update", "Account", account.Id, new { name = account.Name }, cancellationToken);

        return Result.Success(account);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.GetByIdAsync(id, userId, cancellationToken);
        if (account is null)
            return Result.Failure(ErrorCode.NotFound, "Account not found");

        // Regra: recusar exclusão se a conta tiver transações (409)
        var transactionCount = await _accounts.CountTransactionsAsync(account.Id, userId, cancellationToken);
        if (transactionCount > 0)
            return Result.Failure(ErrorCode.Conflict, "Cannot delete account with existing transactions");

        _accounts.Delete(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync("account.delete", "Account", account.Id, new { name = account.Name }, cancellationToken);

        return Result.Success();
    }
}
