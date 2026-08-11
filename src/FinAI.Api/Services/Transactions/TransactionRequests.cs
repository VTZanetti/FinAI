using System.Text.Json.Serialization;

namespace FinAI.Api.Services.Transactions;

public sealed record CreateTransactionRequest(
    Guid AccountId,
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid? CategoryId = null,
    bool IsRecurring = false,
    string? ExternalId = null);

public sealed record UpdateTransactionRequest(
    Guid AccountId,
    string Description,
    decimal Amount,
    DateOnly Date,
    Guid? CategoryId = null,
    bool IsRecurring = false);

/// <summary>
/// Filtro de listagem de transações (query string).
/// </summary>
public sealed record TransactionListQuery(
    Guid? AccountId = null,
    Guid? CategoryId = null,
    string? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? Search = null,
    bool? IsRecurring = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortOrder = null);
