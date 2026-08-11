using FinAI.Api.Models.Enums;

namespace FinAI.Api.Services.Accounts;

public sealed record CreateAccountRequest(
    string Name,
    AccountType Type,
    string Currency = "BRL",
    decimal InitialBalance = 0m);

public sealed record UpdateAccountRequest(
    string Name,
    AccountType Type,
    string Currency);
