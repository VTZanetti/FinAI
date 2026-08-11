namespace FinAI.Api.Services.OpenFinance.Models;

public sealed record PluggyAuthResponse(string AccessToken, int ExpiresAt);

public sealed record PluggyItemDto(string Id, string? InstitutionId, string? ConnectorId);

public sealed record PluggyAccountDto(
    string Id,
    string ItemId,
    string Name,
    string Type,
    string Currency,
    decimal Balance,
    decimal? CreditLimit = null);

public sealed record PluggyTransactionDto(
    string Id,
    string AccountId,
    string Description,
    decimal Amount,
    string Date,
    string? Category,
    string Status);

public sealed record PluggyTransactionPage(
    IReadOnlyList<PluggyTransactionDto> Results,
    int Page,
    int PageSize,
    int TotalPages,
    int Total);

public sealed record PluggyConnectTokenResponse(string AccessToken, int ExpiresAt);