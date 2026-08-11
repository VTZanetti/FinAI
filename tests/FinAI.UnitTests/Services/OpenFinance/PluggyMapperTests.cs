using FinAI.Api.Models.Enums;
using FinAI.Api.Services.OpenFinance;
using FinAI.Api.Services.OpenFinance.Models;
using FluentAssertions;

namespace FinAI.UnitTests.Services.OpenFinance;

[Trait("Category", "Unit")]
public class PluggyMapperTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void ToAccount_MapsFieldsCorrectly()
    {
        // Arrange
        var source = new PluggyAccountDto("acc-1", "item-1", "Conta Corrente", "checking", "BRL", 1250.75m);

        // Act
        var account = PluggyMapper.ToAccount(UserId, source, "Nubank");

        // Assert
        account.UserId.Should().Be(UserId);
        account.Name.Should().Be("Nubank Conta Corrente");
        account.Type.Should().Be(AccountType.Checking);
        account.Currency.Should().Be("BRL");
        account.InitialBalance.Should().Be(1250.75m);
        account.CurrentBalance.Should().Be(1250.75m);
    }

    [Fact]
    public void MapAccountType_CreditCard_ReturnsCreditCard()
    {
        // Act
        var result = PluggyMapper.MapAccountType("credit_card");

        // Assert
        result.Should().Be(AccountType.CreditCard);
    }

    [Fact]
    public void MapAccountType_Unknown_DefaultsToChecking()
    {
        // Act
        var result = PluggyMapper.MapAccountType("poupanca_especial");

        // Assert
        result.Should().Be(AccountType.Checking);
    }

    [Fact]
    public void ToTransaction_NegativeAmount_DerivesExpenseAndKeepsExternalId()
    {
        // Arrange
        var source = new PluggyTransactionDto("tx-1", "acc-1", "Mercado", -250.00m, "2026-08-10", null, "posted");

        // Act
        var tx = PluggyMapper.ToTransaction(UserId, Guid.NewGuid(), source);

        // Assert
        tx.Amount.Should().Be(-250.00m);
        tx.Type.Should().Be(TransactionType.Expense);
        tx.ExternalId.Should().Be("tx-1");
        tx.Date.Should().Be(new DateOnly(2026, 8, 10));
    }

    [Fact]
    public void ToTransaction_PositiveAmount_DerivesIncome()
    {
        // Arrange
        var source = new PluggyTransactionDto("tx-2", "acc-1", "Salário", 5000.00m, "2026-08-05", null, "posted");

        // Act
        var tx = PluggyMapper.ToTransaction(UserId, Guid.NewGuid(), source);

        // Assert
        tx.Type.Should().Be(TransactionType.Income);
    }
}
