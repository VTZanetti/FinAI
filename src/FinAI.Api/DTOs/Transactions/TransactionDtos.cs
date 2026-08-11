using FinAI.Api.Models;
using FinAI.Api.Models.Enums;

namespace FinAI.Api.DTOs.Transactions;

public sealed record TransactionCategoryDto(Guid Id, string Name, string? Subcategory);

public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string Description,
    decimal Amount,
    DateOnly Date,
    TransactionType Type,
    bool IsRecurring,
    TransactionCategoryDto? Category,
    DateTimeOffset CreatedAt);

public sealed record TransactionListItemResponse(
    Guid Id,
    Guid AccountId,
    string Description,
    decimal Amount,
    DateOnly Date,
    TransactionType Type,
    bool IsRecurring,
    TransactionCategoryDto? Category,
    DateTimeOffset CreatedAt);

public static class TransactionMappings
{
    public static TransactionResponse ToResponse(this Transaction t)
        => new(
            t.Id,
            t.AccountId,
            t.Description,
            t.Amount,
            t.Date,
            t.Type,
            t.IsRecurring,
            t.Category is null ? null : new TransactionCategoryDto(t.Category.Id, t.Category.Name, t.Category.Subcategory),
            t.CreatedAt);

    public static TransactionListItemResponse ToListItem(this Transaction t)
        => new(
            t.Id,
            t.AccountId,
            t.Description,
            t.Amount,
            t.Date,
            t.Type,
            t.IsRecurring,
            t.Category is null ? null : new TransactionCategoryDto(t.Category.Id, t.Category.Name, t.Category.Subcategory),
            t.CreatedAt);
}
