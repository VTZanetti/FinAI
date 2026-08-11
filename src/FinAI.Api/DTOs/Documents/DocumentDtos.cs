using FinAI.Api.Models;

namespace FinAI.Api.DTOs.Documents;

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    DocumentStatus Status,
    string? FailureReason,
    int TextLength,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessedAt);

public static class DocumentMappings
{
    public static DocumentResponse ToResponse(this FinancialDocument d)
        => new(d.Id, d.FileName, d.ContentType, d.Status, d.FailureReason, d.TextLength, d.UploadedAt, d.ProcessedAt);
}
