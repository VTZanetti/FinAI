using FinAI.Api.Services.Forecasting;
using FluentAssertions;

namespace FinAI.UnitTests.Services.Forecasting;

[Trait("Category", "Unit")]
public class MovingAverageForecasterTests
{
    private readonly MovingAverageForecaster _forecaster = new();

    [Fact]
    public void ForecastNext_ConstantSeries_ReturnsSameValue()
    {
        // Arrange: série constante 100
        IReadOnlyList<decimal> series = [100m, 100m, 100m, 100m];

        // Act
        var result = _forecaster.ForecastNext(series);

        // Assert
        result.Should().Be(100m);
    }

    [Fact]
    public void ForecastNext_EmptySeries_ReturnsZero()
    {
        // Act
        var result = _forecaster.ForecastNext([]);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void ForecastNext_SingleValue_ReturnsIt()
    {
        // Act
        var result = _forecaster.ForecastNext([42.50m]);

        // Assert
        result.Should().Be(42.50m);
    }

    [Fact]
    public void ForecastNext_WeightsRecentMonthsMore()
    {
        // Arrange: [100, 200, 300] → pesos 1,2,3 → (100*1 + 200*2 + 300*3)/6 = 1400/6 ≈ 233.33
        IReadOnlyList<decimal> series = [100m, 200m, 300m];

        // Act
        var result = _forecaster.ForecastNext(series);

        // Assert
        result.Should().BeApproximately(233.33m, 0.1m);
    }

    [Fact]
    public void ForecastNext_IncreasingSeries_WeightsLatest()
    {
        // Arrange: [100, 100, 100, 1000] → pesos 1..4 → (100+200+300+4000)/10 = 4600/10 = 460
        IReadOnlyList<decimal> series = [100m, 100m, 100m, 1000m];

        // Act
        var result = _forecaster.ForecastNext(series);

        // Assert: o mês recente de 1000 tem peso maior
        result.Should().Be(460m);
    }
}
