using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services.AI;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.Caching;

namespace FinAI.Api.Services.Transactions;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactions;
    private readonly IAccountRepository _accounts;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly IClassificationService _classification;
    private readonly ICacheService _cache;

    public TransactionService(
        ITransactionRepository transactions,
        IAccountRepository accounts,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IClassificationService classification,
        ICacheService cache)
    {
        _transactions = transactions;
        _accounts = accounts;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _classification = classification;
        _cache = cache;
    }

    public async Task<ServiceResult<Transaction>> CreateAsync(Guid userId, CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        // Deduplicação por (UserId, ExternalId) — importações repetidas não duplicam
        if (!string.IsNullOrWhiteSpace(request.ExternalId))
        {
            var exists = await _transactions.ExistsByExternalIdAsync(userId, request.ExternalId.Trim(), cancellationToken);
            if (exists)
                return ServiceResult<Transaction>.Failure(ErrorCode.Conflict, "Transaction with the same ExternalId already exists");
        }

        // Conta deve pertencer ao usuário
        if (!await _accounts.ExistsAsync(request.AccountId, userId, cancellationToken))
            return ServiceResult<Transaction>.Failure(ErrorCode.NotFound, "Account not found");

        // Categoria (se informada) deve pertencer ao usuário (ou ser do sistema)
        if (request.CategoryId.HasValue && await _categories.GetByIdAsync(request.CategoryId.Value, userId, cancellationToken) is null)
            return ServiceResult<Transaction>.Failure(ErrorCode.NotFound, "Category not found");

        // Classificação automática: categoryId ausente → cascata rules → cache → LLM → fallback
        Guid? categoryId = request.CategoryId;
        ClassificationResult? classification = null;
        if (!categoryId.HasValue)
        {
            classification = await _classification.ClassifyAsync(userId, request.Description, request.Amount, cancellationToken);
            categoryId = classification?.CategoryId; // null → transação sem categoria (não bloqueia)
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = request.AccountId,
            CategoryId = categoryId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Date = request.Date,
            Type = DeriveType(request.Amount),
            IsRecurring = request.IsRecurring,
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _lastClassification = classification;

        await _transactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // persiste a transação primeiro

        await RecalculateBalanceAsync(userId, request.AccountId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // depois persiste o saldo recalculado

        var saved = await _transactions.GetByIdAsync(transaction.Id, userId, cancellationToken);

        // Auditoria: valor mascarado pelo AuditDataSanitizer
        await _audit.RecordAsync("transaction.create", "Transaction", transaction.Id,
            new { description = transaction.Description, amount = transaction.Amount, accountId = transaction.AccountId },
            cancellationToken);

        InvalidateAnalyticsCache(userId);

        return ServiceResult<Transaction>.Success(saved ?? transaction, 0, 0, 1, 20);
    }

    /// <summary>
    /// Última classificação automática aplicada (para o controller expor no response).
    /// </summary>
    public ClassificationResult? LastClassification => _lastClassification;

    private ClassificationResult? _lastClassification;

    public async Task<ServiceResult<Transaction>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactions.GetByIdAsync(id, userId, cancellationToken);
        return transaction is null
            ? ServiceResult<Transaction>.Failure(ErrorCode.NotFound, "Transaction not found")
            : ServiceResult<Transaction>.Success(transaction, 0, 0, 1, 20);
    }

    public async Task<ServiceResult<IReadOnlyList<Transaction>>> ListAsync(Guid userId, TransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var result = await _transactions.QueryAsync(userId, filter, cancellationToken);
        return ServiceResult<IReadOnlyList<Transaction>>.Success(result.Items, result.TotalItems, result.TotalPages, result.Page, result.PageSize);
    }

    public async Task<ServiceResult<Transaction>> UpdateAsync(Guid userId, Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactions.GetByIdAsync(id, userId, cancellationToken);
        if (transaction is null)
            return ServiceResult<Transaction>.Failure(ErrorCode.NotFound, "Transaction not found");

        if (!await _accounts.ExistsAsync(request.AccountId, userId, cancellationToken))
            return ServiceResult<Transaction>.Failure(ErrorCode.NotFound, "Account not found");

        if (request.CategoryId.HasValue && await _categories.GetByIdAsync(request.CategoryId.Value, userId, cancellationToken) is null)
            return ServiceResult<Transaction>.Failure(ErrorCode.NotFound, "Category not found");

        var oldAccountId = transaction.AccountId;

        transaction.AccountId = request.AccountId;
        transaction.Description = request.Description.Trim();
        transaction.Amount = request.Amount;
        transaction.Date = request.Date;
        transaction.CategoryId = request.CategoryId;
        transaction.IsRecurring = request.IsRecurring;
        transaction.Type = DeriveType(request.Amount);
        transaction.UpdatedAt = DateTimeOffset.UtcNow;

        _transactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // persiste a transação alterada primeiro

        // Recalcula saldo da conta antiga (se mudou) e da nova — agora com dados persistidos
        await RecalculateBalanceAsync(userId, oldAccountId, cancellationToken);
        if (oldAccountId != request.AccountId)
            await RecalculateBalanceAsync(userId, request.AccountId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken); // persiste os saldos recalculados

        var saved = await _transactions.GetByIdAsync(transaction.Id, userId, cancellationToken);

        await _audit.RecordAsync("transaction.update", "Transaction", transaction.Id,
            new { description = transaction.Description, amount = transaction.Amount, accountId = transaction.AccountId },
            cancellationToken);

        InvalidateAnalyticsCache(userId);

        return ServiceResult<Transaction>.Success(saved ?? transaction, 0, 0, 1, 20);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactions.GetByIdAsync(id, userId, cancellationToken);
        if (transaction is null)
            return Result.Failure(ErrorCode.NotFound, "Transaction not found");

        var accountId = transaction.AccountId;

        _transactions.Delete(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // remove do banco primeiro

        await RecalculateBalanceAsync(userId, accountId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken); // persiste o saldo recalculado

        await _audit.RecordAsync("transaction.delete", "Transaction", id, null, cancellationToken);

        InvalidateAnalyticsCache(userId);

        return Result.Success();
    }

    private void InvalidateAnalyticsCache(Guid userId)
    {
        // Remove as chaves de analytics do usuário (invalidação por evento)
        _cache.RemoveByPrefix($"analytics:spending:{userId}:");
        _cache.RemoveByPrefix($"analytics:behavior:{userId}:");
        _cache.RemoveByPrefix($"analytics:trend:{userId}:");
    }

    private static TransactionType DeriveType(decimal amount)
        => amount >= 0 ? TransactionType.Income : TransactionType.Expense;

    /// <summary>
    /// Recalcula o saldo atual da conta: saldo inicial + soma de todas as transações.
    /// Determinístico — evita drift de saldo incremental.
    /// </summary>
    private async Task RecalculateBalanceAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByIdAsync(accountId, userId, cancellationToken);
        if (account is null)
            return;

        var transactions = await _transactions.QueryAsync(userId, new TransactionFilter(AccountId: accountId, PageSize: int.MaxValue), cancellationToken);
        var sum = transactions.Items.Sum(t => t.Amount);

        account.CurrentBalance = account.InitialBalance + sum;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        _accounts.Update(account);
    }
}
