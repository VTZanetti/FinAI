using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.AI;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.OpenFinance.Models;
using FinAI.Api.Services.OpenFinance.Options;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.OpenFinance;

public sealed record SyncSummary(
    Guid? SyncId,
    int AccountsImported,
    int TransactionsImported,
    int TransactionsSkipped,
    string? Error);

public interface IOpenFinanceSyncService
{
    Task<Result<SyncSummary>> SyncAsync(Guid userId, string? itemId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sincronização Open Finance: apiKey → contas (upsert) → transações (deduplicadas) → classificação IA.
/// </summary>
public class OpenFinanceSyncService : IOpenFinanceSyncService
{
    private readonly IPluggyClient _pluggy;
    private readonly IPluggyAuthService _auth;
    private readonly IOpenFinanceRepository _openFinance;
    private readonly IAccountRepository _accounts;
    private readonly ITransactionRepository _transactions;
    private readonly IClassificationService _classification;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly PluggyOptions _options;
    private readonly ILogger<OpenFinanceSyncService> _logger;

    public OpenFinanceSyncService(
        IPluggyClient pluggy,
        IPluggyAuthService auth,
        IOpenFinanceRepository openFinance,
        IAccountRepository accounts,
        ITransactionRepository transactions,
        IClassificationService classification,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IOptions<PluggyOptions> options,
        ILogger<OpenFinanceSyncService> logger)
    {
        _pluggy = pluggy;
        _auth = auth;
        _openFinance = openFinance;
        _accounts = accounts;
        _transactions = transactions;
        _classification = classification;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<SyncSummary>> SyncAsync(Guid userId, string? itemId = null, CancellationToken cancellationToken = default)
    {
        var sync = new OpenFinanceSync
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartedAt = DateTimeOffset.UtcNow,
            Status = SyncStatus.Running
        };
        await _openFinance.AddSyncAsync(sync, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var effectiveItemId = itemId ?? _options.ItemId;
        if (string.IsNullOrWhiteSpace(effectiveItemId))
        {
            sync.Status = SyncStatus.Failed;
            sync.FinishedAt = DateTimeOffset.UtcNow;
            sync.Error = "No Pluggy ItemId configured";
            _openFinance.UpdateSync(sync);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<SyncSummary>(ErrorCode.Validation, sync.Error);
        }

        try
        {
            var apiKey = await _auth.GetApiKeyAsync(cancellationToken);

            // 1. Contas → upsert por (UserId, ExternalId)
            var pluggyAccounts = await _pluggy.GetAccountsAsync(apiKey, effectiveItemId, cancellationToken);
            var accountsImported = 0;

            foreach (var pluggyAccount in pluggyAccounts)
            {
                await UpsertAccountAsync(userId, pluggyAccount, cancellationToken);
                accountsImported++;
            }

            // 2. Transações (paginado, ImportSinceDays)
            var transactionsImported = 0;
            var transactionsSkipped = 0;
            var since = DateOnly.FromDateTime(DateTime.Today).AddDays(-_options.ImportSinceDays);

            foreach (var pluggyAccount in pluggyAccounts)
            {
                var finAiAccount = await FindAccountByExternalIdAsync(userId, pluggyAccount.Id, cancellationToken);
                if (finAiAccount is null)
                    continue;

                var page = 1;
                PluggyTransactionPage pageResult;
                do
                {
                    pageResult = await _pluggy.GetTransactionsAsync(apiKey, effectiveItemId, page, _options.PageSize, cancellationToken);

                    foreach (var pluggyTx in pageResult.Results)
                    {
                        if (DateOnly.TryParse(pluggyTx.Date, out var txDate) && txDate < since)
                            continue;

                        if (await _transactions.ExistsByExternalIdAsync(userId, pluggyTx.Id, cancellationToken))
                        {
                            transactionsSkipped++;
                            continue;
                        }

                        var transaction = PluggyMapper.ToTransaction(userId, finAiAccount.Id, pluggyTx);

                        // Classificação automática (regras → cache → LLM)
                        if (_options.AutoClassify)
                        {
                            var classification = await _classification.ClassifyAsync(userId, transaction.Description, transaction.Amount, cancellationToken);
                            transaction.CategoryId = classification.CategoryId;
                        }

                        await _transactions.AddAsync(transaction, cancellationToken);
                        transactionsImported++;
                    }

                    page++;
                }
                while (page <= pageResult.TotalPages);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 3. Recalcular saldos das contas importadas
            foreach (var pluggyAccount in pluggyAccounts)
            {
                var finAiAccount = await FindAccountByExternalIdAsync(userId, pluggyAccount.Id, cancellationToken);
                if (finAiAccount is not null)
                {
                    var query = await _transactions.QueryAsync(userId, new TransactionFilter(AccountId: finAiAccount.Id, PageSize: int.MaxValue), cancellationToken);
                    finAiAccount.CurrentBalance = finAiAccount.InitialBalance + query.Items.Sum(t => t.Amount);
                    _accounts.Update(finAiAccount);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // 4. Finaliza o sync
            sync.Status = SyncStatus.Success;
            sync.FinishedAt = DateTimeOffset.UtcNow;
            sync.AccountsImported = accountsImported;
            sync.TransactionsImported = transactionsImported;
            sync.TransactionsSkipped = transactionsSkipped;
            _openFinance.UpdateSync(sync);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _audit.RecordAsync("openfinance.sync", "OpenFinanceSync", sync.Id,
                new { accountsImported, transactionsImported, transactionsSkipped }, cancellationToken);

            return Result.Success(new SyncSummary(sync.Id, accountsImported, transactionsImported, transactionsSkipped, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open Finance sync failed for user {UserId}", userId);

            sync.Status = SyncStatus.Failed;
            sync.FinishedAt = DateTimeOffset.UtcNow;
            sync.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            _openFinance.UpdateSync(sync);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);

            await _audit.RecordAsync("openfinance.sync", "OpenFinanceSync", sync.Id, new { error = "***" }, CancellationToken.None);

            return Result.Failure<SyncSummary>(ErrorCode.Internal, sync.Error);
        }
    }

    private async Task UpsertAccountAsync(Guid userId, PluggyAccountDto source, CancellationToken cancellationToken)
    {
        var existing = await _accounts.FindByExternalIdAsync(userId, source.Id, cancellationToken);
        if (existing is not null)
        {
            existing.CurrentBalance = source.Balance;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            _accounts.Update(existing);
            return;
        }

        var account = PluggyMapper.ToAccount(userId, source, string.Empty);
        account.ExternalId = source.Id;
        await _accounts.AddAsync(account, cancellationToken);
    }

    private async Task<Account?> FindAccountByExternalIdAsync(Guid userId, string externalId, CancellationToken cancellationToken)
        => await _accounts.FindByExternalIdAsync(userId, externalId, cancellationToken);
}