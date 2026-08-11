using FinAI.Api.Models;
using FinAI.Api.Models.Enums;

namespace FinAI.Api.DTOs.Transactions;

public sealed record TransactionCategoryDto(Guid Id, string Name, string? Subcategory);

public sealed record ClassificationDto(string Category, string? Subcategory, decimal Confidence, string Source);

public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string Description,
    decimal Amount,
    DateOnly Date,
    TransactionType Type,
    bool IsRecurring,
    TransactionCategoryDto? Category,
    ClassificationDto? Classification,
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
    ClassificationDto? Classification,
    DateTimeOffset CreatedAt);

public static class TransactionMappings
{
    public static TransactionResponse ToResponse(this Transaction t, Services.AI.ClassificationResult? classification = null)
        => new(
            t.Id,
            t.AccountId,
            t.Description,
            t.Amount,
            t.Date,
            t.Type,
            t.IsRecurring,
            t.Category is null ? null : new TransactionCategoryDto(t.Category.Id, t.Category.Name, t.Category.Subcategory),
            classification is null
                ? null
                : new ClassificationDto(classification.Category, classification.Subcategory, classification.Confidence, classification.Source),
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
            null,
            t.CreatedAt);
}
