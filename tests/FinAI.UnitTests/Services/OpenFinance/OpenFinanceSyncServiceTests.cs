using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.AI;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.OpenFinance;
using FinAI.Api.Services.OpenFinance.Models;
using FinAI.Api.Services.OpenFinance.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinAI.UnitTests.Services.OpenFinance;

[Trait("Category", "Unit")]
public class OpenFinanceSyncServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IPluggyClient _pluggy = Substitute.For<IPluggyClient>();
    private readonly IPluggyAuthService _auth = Substitute.For<IPluggyAuthService>();
    private readonly IOpenFinanceRepository _openFinance = Substitute.For<IOpenFinanceRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IClassificationService _classification = Substitute.For<IClassificationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();

    private OpenFinanceSyncService CreateService()
        => new(_pluggy, _auth, _openFinance, _accounts, _transactions, _classification, _unitOfWork, _audit,
            Options.Create(new PluggyOptions { ItemId = "item-1", ImportSinceDays = 90, AutoClassify = true }),
            NullLogger<OpenFinanceSyncService>.Instance);

    [Fact]
    public async Task SyncAsync_NoItemId_ReturnsValidationError()
    {
        // Arrange
        var service = new OpenFinanceSyncService(_pluggy, _auth, _openFinance, _accounts, _transactions,
            _classification, _unitOfWork, _audit,
            Options.Create(new PluggyOptions { ItemId = "" }),
            NullLogger<OpenFinanceSyncService>.Instance);

        // Act
        var result = await service.SyncAsync(UserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(FinAI.Api.Common.ErrorCode.Validation);
    }

    [Fact]
    public async Task SyncAsync_ImportsAccountsAndTransactions_WithDeduplication()
    {
        // Arrange
        _auth.GetApiKeyAsync(Arg.Any<CancellationToken>()).Returns("api-key");
        _pluggy.GetAccountsAsync("api-key", "item-1", Arg.Any<CancellationToken>())
            .Returns(new List<PluggyAccountDto>
            {
                new("acc-1", "item-1", "Conta", "checking", "BRL", 1000m)
            });
        _pluggy.GetTransactionsAsync("api-key", "item-1", 1, 100, Arg.Any<CancellationToken>())
            .Returns(new PluggyTransactionPage(
                new List<PluggyTransactionDto>
                {
                    new("tx-1", "acc-1", "Mercado", -150m, "2026-08-01", null, "posted"),
                    new("tx-2", "acc-1", "Salário", 3000m, "2026-08-05", null, "posted")
                }, 1, 100, 1, 2));
        _pluggy.GetTransactionsAsync("api-key", "item-1", 2, 100, Arg.Any<CancellationToken>())
            .Returns(new PluggyTransactionPage([], 2, 100, 1, 2));

        // Primeira chamada: conta não existe (upsert cria); nas seguintes: existe (para as transações)
        var createdAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Name = "Nubank Conta",
            Type = AccountType.Checking,
            Currency = "BRL",
            InitialBalance = 1000m,
            CurrentBalance = 1000m,
            ExternalId = "acc-1"
        };
        var accountLookups = 0;
        _accounts.FindByExternalIdAsync(UserId, "acc-1", Arg.Any<CancellationToken>())
            .Returns(callInfo => ++accountLookups == 1 ? (Account?)null : createdAccount);
        _transactions.QueryAsync(UserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([], 0, 1, int.MaxValue, 1));

        // tx-1 já existe (deduplicação); tx-2 é nova
        _transactions.ExistsByExternalIdAsync(UserId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<string>() == "tx-1");

        _classification.ClassifyAsync(UserId, Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(new ClassificationResult(Guid.NewGuid(), "Food", null, 0.8m, "rules"));

        var service = CreateService();

        // Act
        var result = await service.SyncAsync(UserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccountsImported.Should().Be(1);
        result.Value.TransactionsImported.Should().Be(1); // tx-2 importada, tx-1 skippada
        result.Value.TransactionsSkipped.Should().Be(1);
        await _audit.Received().RecordAsync("openfinance.sync", "OpenFinanceSync", Arg.Any<Guid?>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_PluggyFailure_MarksSyncFailed()
    {
        // Arrange
        _auth.GetApiKeyAsync(Arg.Any<CancellationToken>()).Returns("api-key");
        _pluggy.GetAccountsAsync("api-key", "item-1", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<PluggyAccountDto>>>(_ => throw new HttpRequestException("Pluggy down"));

        var service = CreateService();

        // Act
        var result = await service.SyncAsync(UserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(FinAI.Api.Common.ErrorCode.Internal);
    }
}
