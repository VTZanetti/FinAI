using FinAI.Api.Models;
using FinAI.Api.Models.Enums;
using FinAI.Api.Services.OpenFinance.Models;

namespace FinAI.Api.Services.OpenFinance;

/// <summary>
/// Mapeamento Pluggy → FinAI (contas e transações).
/// </summary>
public static class PluggyMapper
{
    public static Account ToAccount(Guid userId, PluggyAccountDto source, string institutionName)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = $"{institutionName} {source.Name}".Trim(),
            Type = MapAccountType(source.Type),
            Currency = source.Currency,
            InitialBalance = source.Balance,
            CurrentBalance = source.Balance,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Transaction ToTransaction(Guid userId, Guid accountId, PluggyTransactionDto source)
    {
        var date = DateOnly.TryParse(source.Date, out var parsed) ? parsed : DateOnly.FromDateTime(DateTime.Today);

        return new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            Description = source.Description,
            Amount = source.Amount,
            Date = date,
            Type = source.Amount >= 0 ? TransactionType.Income : TransactionType.Expense,
            ExternalId = source.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static AccountType MapAccountType(string pluggyType)
        => pluggyType.ToLowerInvariant() switch
        {
            "checking" => AccountType.Checking,
            "savings" => AccountType.Savings,
            "credit" or "credit_card" => AccountType.CreditCard,
            "cash" => AccountType.Cash,
            "investment" => AccountType.Investment,
            _ => AccountType.Checking
        };
}