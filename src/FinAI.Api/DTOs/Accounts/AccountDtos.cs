using FinAI.Api.Models;
using FinAI.Api.Models.Enums;

namespace FinAI.Api.DTOs.Accounts;

public sealed record AccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    decimal InitialBalance,
    decimal CurrentBalance,
    DateTimeOffset CreatedAt);

public sealed record AccountListItemResponse(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    decimal CurrentBalance,
    DateTimeOffset CreatedAt);

public static class AccountMappings
{
    public static AccountResponse ToResponse(this Account account)
        => new(account.Id, account.Name, account.Type, account.Currency, account.InitialBalance, account.CurrentBalance, account.CreatedAt);

    public static AccountListItemResponse ToListItem(this Account account)
        => new(account.Id, account.Name, account.Type, account.Currency, account.CurrentBalance, account.CreatedAt);
}
