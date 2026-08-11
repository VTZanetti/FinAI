using FinAI.Api.Services.AnomalyDetection;
using FluentAssertions;

namespace FinAI.UnitTests.Services.AnomalyDetection;

[Trait("Category", "Unit")]
public class IqrAnomalyDetectorTests
{
    [Fact]
    public void Assess_ValueAboveUpperBound_ReturnsAnomaly()
    {
        // Arrange: Q1=100, Q3=200, IQR=100, limite superior = 200 + 1.5*100 = 350
        IReadOnlyList<decimal> history = [100m, 110m, 120m, 130m, 180m, 190m, 200m, 210m];
        var detector = new IqrAnomalyDetector(minSamples: 4);

        // Act
        var result = detector.Assess(1000m, history);

        // Assert
        result.Anomaly.Should().BeTrue();
        result.Method.Should().Be("iqr");
        result.Reason.Should().Contain("outlier");
    }

    [Fact]
    public void Assess_ValueWithinBounds_ReturnsNoAnomaly()
    {
        // Arrange
        IReadOnlyList<decimal> history = [100m, 110m, 120m, 130m, 180m, 190m, 200m, 210m];
        var detector = new IqrAnomalyDetector(minSamples: 4);

        // Act
        var result = detector.Assess(200m, history);

        // Assert
        result.Anomaly.Should().BeFalse();
    }

    [Fact]
    public void Assess_FewerThanMinSamples_ReturnsInsufficientData()
    {
        // Arrange
        var detector = new IqrAnomalyDetector(minSamples: 4);

        // Act: apenas 2 amostras
        var result = detector.Assess(999m, [100m, 110m]);

        // Assert
        result.Anomaly.Should().BeFalse();
        result.Reason.Should().Contain("Insufficient");
    }

    [Fact]
    public void Percentile_CalculatesQuartilesCorrectly()
    {
        // Arrange: 8 valores
        IReadOnlyList<decimal> sorted = [100m, 110m, 120m, 130m, 180m, 190m, 200m, 210m];

        // Act
        var q1 = IqrAnomalyDetector.Percentile(sorted, 0.25);
        var q3 = IqrAnomalyDetector.Percentile(sorted, 0.75);

        // Assert: (n-1)*p = 1.75 → 110 + 0.75*10 = 117.5; 5.25 → 190 + 0.25*10 = 192.5
        q1.Should().BeApproximately(117.5m, 0.1m);
        q3.Should().BeApproximately(192.5m, 0.1m);
    }

    [Fact]
    public void Assess_ValueBelowLowerBound_ReturnsAnomaly()
    {
        // Arrange: Q1=100, IQR=100, limite inferior = 100 - 150 = -50 → valores negativos seriam outliers
        IReadOnlyList<decimal> history = [100m, 110m, 120m, 130m, 180m, 190m, 200m, 210m];
        var detector = new IqrAnomalyDetector(minSamples: 4);

        // Act: 500 acima do limite → anomalia (positive side)
        var result = detector.Assess(500m, history);

        // Assert
        result.Anomaly.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0m);
    }
}
