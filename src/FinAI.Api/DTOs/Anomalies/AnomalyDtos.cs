using FinAI.Api.Services.AnomalyDetection;
using FinAI.Api.Services.AnomalyDetection.Models;

namespace FinAI.Api.DTOs.Anomalies;

public sealed record AnomalyReportResponse(string Method, IReadOnlyList<AnomalyItemDto> Items);

public sealed record AnomalyItemDto(
    Guid TransactionId,
    string Description,
    decimal Amount,
    DateOnly Date,
    string? Category,
    bool Anomaly,
    decimal Score,
    string Reason);

public sealed record AnomalyCheckRequest(string Description, decimal Amount, Guid? CategoryId = null);

public sealed record AnomalyCheckResponse(bool Anomaly, decimal Score, string Reason, string SuggestedAction, string Method);

public static class AnomalyMappings
{
    public static AnomalyReportResponse ToResponse(this AnomalyDetectionReport r)
        => new(r.Method, r.Items.Select(i => new AnomalyItemDto(
            i.TransactionId, i.Description, i.Amount, i.Date, i.Category, i.Anomaly, i.Score, i.Reason)).ToList());

    public static AnomalyCheckResponse ToResponse(this AnomalyCheckResult r)
        => new(r.Anomaly, r.Score, r.Reason, r.SuggestedAction, r.Method);
}
