namespace FinAI.Api.Services.AnomalyDetection;

/// <summary>
/// Detecção por IQR: outlier se x &lt; Q1 - 1.5*IQR ou x &gt; Q3 + 1.5*IQR.
/// Score derivado da distância normalizada ao limite.
/// </summary>
public class IqrAnomalyDetector : IAnomalyDetector
{
    public const string MethodNameValue = "iqr";

    private const double IqrMultiplier = 1.5;
    private readonly int _minSamples;

    public IqrAnomalyDetector(int minSamples)
    {
        _minSamples = minSamples;
    }

    public string MethodName => MethodNameValue;

    public bool HasEnoughSamples(int count) => count >= _minSamples;

    public AnomalyAssessment Assess(decimal value, IReadOnlyList<decimal> history)
    {
        if (!HasEnoughSamples(history.Count))
            return new AnomalyAssessment(false, 0m, "Insufficient data for IQR analysis", MethodName);

        var sorted = history.OrderBy(x => x).ToList();
        var q1 = Percentile(sorted, 0.25);
        var q3 = Percentile(sorted, 0.75);
        var iqr = q3 - q1;

        if (iqr == 0m)
            return new AnomalyAssessment(false, 0m, "No spread in historical data", MethodName);

        var lowerBound = q1 - (decimal)IqrMultiplier * iqr;
        var upperBound = q3 + (decimal)IqrMultiplier * iqr;

        var isAnomaly = value < lowerBound || value > upperBound;

        // Score: distância ao limite mais próximo, normalizado
        var distance = isAnomaly
            ? Math.Min(Math.Abs(value - lowerBound), Math.Abs(value - upperBound))
            : 0m;
        var score = isAnomaly ? Math.Min(1m, distance / (iqr * 2m)) : 0m;
        score = Math.Round(score, 2);

        var reason = isAnomaly
            ? "Amount is an outlier compared to historical spending (IQR)"
            : "Amount is within historical spending pattern";

        return new AnomalyAssessment(isAnomaly, score, reason, MethodName);
    }

    internal static decimal Percentile(IReadOnlyList<decimal> sorted, double percentile)
    {
        if (sorted.Count == 0)
            return 0m;

        var index = (sorted.Count - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper)
            return sorted[lower];

        var fraction = index - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (decimal)fraction;
    }
}
