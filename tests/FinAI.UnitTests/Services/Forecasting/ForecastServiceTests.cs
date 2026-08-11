using FinAI.Api.Common;
using FinAI.Api.Repositories;
using FinAI.Api.Services.Forecasting;
using FinAI.Api.Services.Forecasting.Models;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Forecasting;

[Trait("Category", "Unit")]
public class ForecastServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IAnalyticsRepository _analytics = Substitute.For<IAnalyticsRepository>();
    private readonly IMovingAverageForecaster _forecaster = Substitute.For<IMovingAverageForecaster>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();

    private ForecastService CreateService() => new(_analytics, _forecaster, _transactions);

    [Fact]
    public async Task GetCashFlowForecastAsync_InvalidMonths_ReturnsValidationError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetCashFlowForecastAsync(UserId, 0);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public async Task GetCashFlowForecastAsync_ReturnsMethodAndConfidence()
    {
        // Arrange
        _analytics.GetMonthlyTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyAggregate> { new(2026, 7, 5000m, 3000m), new(2026, 8, 5500m, 3200m) });
        _analytics.GetRecurringExpensesAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(900m); // 300/mês
        _forecaster.ForecastNext(Arg.Any<IReadOnlyList<decimal>>()).Returns(4000m, 3000m);
        var service = CreateService();

        // Act
        var result = await service.GetCashFlowForecastAsync(UserId, 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Method.Should().Be("weighted_moving_average");
        result.Value.Forecast.Should().HaveCount(3);
        result.Value.Confidence.Level.Should().Be("low"); // 2 meses de histórico
        result.Value.Forecast[0].Month.Should().NotBeNullOrWhiteSpace();
        result.Value.Forecast[0].Income.Should().Be(4000m);
    }

    [Fact]
    public async Task GetCashFlowForecastAsync_RecurringExpenses_AreGuaranteedFloor()
    {
        // Arrange: despesa prevista baixa (100) mas recorrência mensal alta (500)
        _analytics.GetMonthlyTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyAggregate> { new(2026, 8, 5000m, 3000m) });
        _analytics.GetRecurringExpensesAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(1500m); // 500/mês
        _forecaster.ForecastNext(Arg.Any<IReadOnlyList<decimal>>()).Returns(5000m, 100m);
        var service = CreateService();

        // Act
        var result = await service.GetCashFlowForecastAsync(UserId, 1);

        // Assert: despesa garantida pelo piso de recorrência (500)
        result.Value!.Forecast[0].Expenses.Should().Be(500m);
        result.Value.Forecast[0].Balance.Should().Be(4500m); // 5000 - 500
    }

    [Fact]
    public async Task GetCashFlowForecastAsync_EmptyHistory_ReturnsZeroForecast()
    {
        // Arrange
        _analytics.GetMonthlyTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyAggregate>());
        _analytics.GetRecurringExpensesAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(0m);
        _forecaster.ForecastNext(Arg.Any<IReadOnlyList<decimal>>()).Returns(0m);
        var service = CreateService();

        // Act
        var result = await service.GetCashFlowForecastAsync(UserId, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Forecast.Should().HaveCount(2);
        result.Value.Forecast.All(p => p.Income == 0m && p.Expenses == 0m).Should().BeTrue();
    }
}
