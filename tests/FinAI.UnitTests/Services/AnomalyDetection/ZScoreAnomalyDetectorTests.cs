using FinAI.Api.Services.AnomalyDetection;
using FluentAssertions;

namespace FinAI.UnitTests.Services.AnomalyDetection;

[Trait("Category", "Unit")]
public class ZScoreAnomalyDetectorTests
{
    [Fact]
    public void Assess_ExtremeValue_ReturnsAnomaly()
    {
        // Arrange: valores ~150-210 com um extremo 1280
        var history = BuildHistory();
        var detector = new ZScoreAnomalyDetector(minSamples: 8, threshold: 3.0);

        // Act
        var result = detector.Assess(1280m, history);

        // Assert
        result.Anomaly.Should().BeTrue();
        result.Score.Should().BeInRange(0m, 1m);
        result.Method.Should().Be("zscore");
        result.Reason.Should().Contain("exceeds");
    }

    [Fact]
    public void Assess_NormalValue_ReturnsNoAnomaly()
    {
        // Arrange
        var history = BuildHistory();
        var detector = new ZScoreAnomalyDetector(minSamples: 8, threshold: 3.0);

        // Act
        var result = detector.Assess(180m, history);

        // Assert
        result.Anomaly.Should().BeFalse();
    }

    [Fact]
    public void Assess_FewerThanMinSamples_ReturnsInsufficientData()
    {
        // Arrange: apenas 4 amostras (< 8)
        var detector = new ZScoreAnomalyDetector(minSamples: 8, threshold: 3.0);

        // Act
        var result = detector.Assess(500m, [100m, 150m, 120m, 130m]);

        // Assert
        result.Anomaly.Should().BeFalse();
        result.Reason.Should().Contain("Insufficient");
    }

    [Fact]
    public void Assess_NoVariance_ReturnsNoAnomaly()
    {
        // Arrange: série constante
        var detector = new ZScoreAnomalyDetector(minSamples: 8, threshold: 3.0);

        // Act
        var result = detector.Assess(100m, Enumerable.Repeat(100m, 10).ToList());

        // Assert
        result.Anomaly.Should().BeFalse();
        result.Reason.Should().Contain("variance");
    }

    [Fact]
    public void Assess_Score_IsNormalizedBetweenZeroAndOne()
    {
        // Arrange
        var history = BuildHistory();
        var detector = new ZScoreAnomalyDetector(minSamples: 8, threshold: 3.0);

        // Act: valor extremo grande
        var result = detector.Assess(50000m, history);

        // Assert: score não passa de 1
        result.Score.Should().BeLessThanOrEqualTo(1m);
        result.Score.Should().BeGreaterThanOrEqualTo(0m);
    }

    private static List<decimal> BuildHistory()
    {
        var values = new List<decimal>();
        var random = new Random(42);
        for (var i = 0; i < 24; i++)
            values.Add(150m + random.Next(0, 60));

        return values;
    }
}
