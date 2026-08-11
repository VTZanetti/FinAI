using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.Accounts;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Accounts;

[Trait("Category", "Unit")]
public class AccountServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private AccountService CreateService() => new(_accounts, _unitOfWork);

    [Fact]
    public async Task CreateAsync_WithInitialBalance_SetsCurrentBalanceEqual()
    {
        // Arrange
        var service = CreateService();
        var request = new CreateAccountRequest("Conta Corrente", AccountType.Checking, "BRL", 1250.75m);

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.InitialBalance.Should().Be(1250.75m);
        result.Value.CurrentBalance.Should().Be(1250.75m); // regra: saldo inicial = saldo atual
        result.Value.UserId.Should().Be(UserId);
        result.Value.Currency.Should().Be("BRL");
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_CurrencyNormalizedToUpper()
    {
        // Arrange
        var service = CreateService();
        var request = new CreateAccountRequest("Conta", AccountType.Cash, "brl");

        // Act
        var result = await service.CreateAsync(UserId, request);

        // Assert
        result.Value!.Currency.Should().Be("BRL");
    }

    [Fact]
    public async Task GetByIdAsync_AccountNotOwned_ReturnsNotFound()
    {
        // Arrange
        _accounts.GetByIdAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns((Account?)null);
        var service = CreateService();

        // Act
        var result = await service.GetByIdAsync(UserId, AccountId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound); // 404 — nunca 403
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeInitialBalance()
    {
        // Arrange
        var account = new Account
        {
            Id = AccountId,
            UserId = UserId,
            Name = "Antiga",
            Type = AccountType.Checking,
            Currency = "BRL",
            InitialBalance = 100m,
            CurrentBalance = 250m
        };
        _accounts.GetByIdAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(account);
        var service = CreateService();

        var request = new UpdateAccountRequest("Nova", AccountType.Savings, "USD");

        // Act
        var result = await service.UpdateAsync(UserId, AccountId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Nova");
        result.Value.Type.Should().Be(AccountType.Savings);
        result.Value.Currency.Should().Be("USD");
        result.Value.InitialBalance.Should().Be(100m); // não editável
        result.Value.CurrentBalance.Should().Be(250m);
    }

    [Fact]
    public async Task DeleteAsync_AccountWithTransactions_ReturnsConflict()
    {
        // Arrange
        var account = new Account { Id = AccountId, UserId = UserId, Name = "Conta" };
        _accounts.GetByIdAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(account);
        _accounts.CountTransactionsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(3);
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, AccountId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Conflict); // 409
        _accounts.DidNotReceive().Delete(account);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_AccountWithoutTransactions_Deletes()
    {
        // Arrange
        var account = new Account { Id = AccountId, UserId = UserId, Name = "Conta" };
        _accounts.GetByIdAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(account);
        _accounts.CountTransactionsAsync(AccountId, UserId, Arg.Any<CancellationToken>()).Returns(0);
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, AccountId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _accounts.Received().Delete(account);
        await _unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
