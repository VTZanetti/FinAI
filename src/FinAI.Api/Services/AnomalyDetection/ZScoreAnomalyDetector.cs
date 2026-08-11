namespace FinAI.Api.Services.AnomalyDetection;

public sealed record AnomalyAssessment(bool Anomaly, decimal Score, string Reason, string Method);

public interface IAnomalyDetector
{
    string MethodName { get; }
    bool HasEnoughSamples(int count);
    AnomalyAssessment Assess(decimal value, IReadOnlyList<decimal> history);
}

/// <summary>
/// Detecção por Z-score: |z| > threshold → anomalia. Score = min(1, |z|/5).
/// </summary>
public class ZScoreAnomalyDetector : IAnomalyDetector
{
    public const string MethodNameValue = "zscore";

    private readonly int _minSamples;
    private readonly double _threshold;

    public ZScoreAnomalyDetector(int minSamples, double threshold)
    {
        _minSamples = minSamples;
        _threshold = threshold;
    }

    public string MethodName => MethodNameValue;

    public bool HasEnoughSamples(int count) => count >= _minSamples;

    public AnomalyAssessment Assess(decimal value, IReadOnlyList<decimal> history)
    {
        if (!HasEnoughSamples(history.Count))
            return new AnomalyAssessment(false, 0m, "Insufficient data for Z-score analysis", MethodName);

        var mean = history.Average();
        var variance = history.Sum(x => (x - mean) * (x - mean)) / history.Count;
        var std = (decimal)Math.Sqrt((double)variance);

        if (std == 0m)
            return new AnomalyAssessment(false, 0m, "No variance in historical data", MethodName);

        var z = (double)((value - mean) / std);
        var isAnomaly = Math.Abs(z) > _threshold;

        var score = (decimal)Math.Min(1.0, Math.Abs(z) / 5.0);
        score = Math.Round(score, 2);

        var reason = isAnomaly
            ? "Amount significantly exceeds historical spending pattern"
            : "Amount is within historical spending pattern";

        return new AnomalyAssessment(isAnomaly, score, reason, MethodName);
    }
}
