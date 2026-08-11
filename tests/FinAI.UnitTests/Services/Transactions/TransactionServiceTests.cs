using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.Transactions;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Transactions;

[Trait("Category", "Unit")]
public class TransactionServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CategoryId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();

    private TransactionService CreateService() => new(_transactions, _accounts, _categories, _unitOfWork, _audit);

    private static Account Account(decimal initial = 0m) => new()
    {
        Id = AccountId,
        UserId = UserId,
        Name = "Conta",
        Currency = "BRL",
        InitialBalance = initial,
        CurrentBalance = initial
    };

    private static Transaction Transaction(decimal amount, Guid? categoryId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        AccountId = AccountId,
        CategoryId = categoryId,
        Description = "TESTE",
        Amount = amount,
        Date = new DateOnly(2026, 8, 10),
        Type = amount >= 0 ? TransactionType.Income : TransactionType.Expense,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task CreateAsync_NegativeAmount_DerivesExpenseType()
    {
        // Arrange
        _accounts.ExistsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(true);
        _transactions.QueryAsync(Arg.Any<Guid>(), Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([], 0, 1, 20, 0));

        var service = CreateService();
        var request = new CreateTransactionRequest(AccountId, "Mercado", -150.50m, new DateOnly(2026, 8, 10));

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(TransactionType.Expense);
    }

    [Fact]
    public async Task CreateAsync_PositiveAmount_DerivesIncomeType()
    {
        // Arrange
        _accounts.ExistsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(true);
        _transactions.QueryAsync(Arg.Any<Guid>(), Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([], 0, 1, 20, 0));

        var service = CreateService();
        var request = new CreateTransactionRequest(AccountId, "Salário", 5000.00m, new DateOnly(2026, 8, 10));

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(TransactionType.Income);
    }

    [Fact]
    public async Task CreateAsync_DuplicateExternalId_ReturnsConflict()
    {
        // Arrange
        _transactions.ExistsByExternalIdAsync(UserId, "ext-001", Arg.Any<CancellationToken>()).Returns(true);

        var service = CreateService();
        var request = new CreateTransactionRequest(AccountId, "Dup", -10m, new DateOnly(2026, 8, 10), ExternalId: "ext-001");

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Conflict);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_AccountNotOwnedByUser_ReturnsNotFound()
    {
        // Arrange
        _accounts.ExistsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(false);

        var service = CreateService();
        var request = new CreateTransactionRequest(AccountId, "Alheia", -10m, new DateOnly(2026, 8, 10));

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound); // 404 — nunca 403
    }

    [Fact]
    public async Task CreateAsync_CategoryNotOwnedByUser_ReturnsNotFound()
    {
        // Arrange
        _accounts.ExistsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(true);
        _categories.GetByIdAsync(CategoryId, UserId, Arg.Any<CancellationToken>()).Returns((Category?)null);

        var service = CreateService();
        var request = new CreateTransactionRequest(AccountId, "Teste", -10m, new DateOnly(2026, 8, 10), CategoryId: CategoryId);

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_RecalculatesAccountBalance()
    {
        // Arrange
        var account = Account(initial: 1000m);
        _accounts.ExistsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(true);
        _accounts.GetByIdAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(account);
        _transactions.QueryAsync(UserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult(
                [Transaction(-150m), Transaction(-50m), Transaction(500m)],
                3, 1, 20, 1));

        var service = CreateService();
        var request = new CreateTransactionRequest(AccountId, "Gastos", -150m, new DateOnly(2026, 8, 10));

        // Act
        await service.CreateAsync(UserId, request);

        // Assert: 1000 + (-150) + (-50) + 500 = 1300
        account.CurrentBalance.Should().Be(1300m);
        _accounts.Received().Update(account);
    }

    [Fact]
    public async Task UpdateAsync_TransactionNotOwned_ReturnsNotFound()
    {
        // Arrange
        _transactions.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>()).Returns((Transaction?)null);

        var service = CreateService();
        var request = new UpdateTransactionRequest(AccountId, "X", -1m, new DateOnly(2026, 8, 10));

        // Act
        var result = await service.UpdateAsync(UserId, Guid.NewGuid(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_MovingAccount_RecalculatesBothAccounts()
    {
        // Arrange
        var oldAccountId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var newAccountId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var transaction = Transaction(-100m);
        transaction.AccountId = oldAccountId;
        _transactions.GetByIdAsync(transaction.Id, UserId, Arg.Any<CancellationToken>()).Returns(transaction);
        _accounts.ExistsAsync(newAccountId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        _accounts.GetByIdAsync(oldAccountId, UserId, Arg.Any<CancellationToken>()).Returns(Account());
        _accounts.GetByIdAsync(newAccountId, UserId, Arg.Any<CancellationToken>()).Returns(Account());
        _transactions.QueryAsync(UserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([transaction], 1, 1, 20, 1));

        var service = CreateService();
        var request = new UpdateTransactionRequest(newAccountId, "Movida", -100m, new DateOnly(2026, 8, 10));

        // Act
        var result = await service.UpdateAsync(UserId, transaction.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        transaction.AccountId.Should().Be(newAccountId);
        _accounts.Received(2).Update(Arg.Any<Account>());
    }

    [Fact]
    public async Task DeleteAsync_TransactionNotOwned_ReturnsNotFound()
    {
        // Arrange
        _transactions.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>()).Returns((Transaction?)null);

        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ValidTransaction_DeletesAndRecalculatesBalance()
    {
        // Arrange
        var transaction = Transaction(-100m);
        var account = Account(initial: 500m);
        _transactions.GetByIdAsync(transaction.Id, UserId, Arg.Any<CancellationToken>()).Returns(transaction);
        _accounts.GetByIdAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(account);
        _transactions.QueryAsync(UserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionQueryResult([], 0, 1, 20, 0));

        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, transaction.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _transactions.Received().Delete(transaction);
        account.CurrentBalance.Should().Be(500m); // sem transações restantes
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
