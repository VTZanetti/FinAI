namespace FinAI.Api.Services.AnomalyDetection.Models;

public sealed record AnomalyResult(
    Guid TransactionId,
    string Description,
    decimal Amount,
    DateOnly Date,
    string? Category,
    bool Anomaly,
    decimal Score,
    string Reason,
    string Method);

public sealed record AnomalyCheckResult(
    bool Anomaly,
    decimal Score,
    string Reason,
    string SuggestedAction,
    string Method);

public sealed record AnomalyDetectionOptions
{
    public const string SectionName = "Analytics";

    public int MinSamplesForZScore { get; set; } = 8;
    public double AnomalyZScoreThreshold { get; set; } = 3.0;
    public int MinSamplesForIqr { get; set; } = 4;
}
