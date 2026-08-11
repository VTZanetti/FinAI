using FinAI.Api.Repositories;
using FinAI.Api.Services.Analytics;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Analytics;

[Trait("Category", "Unit")]
public class MonthlyTrendAnalyzerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IAnalyticsRepository _repository = Substitute.For<IAnalyticsRepository>();

    private MonthlyTrendAnalyzer CreateAnalyzer() => new(_repository);

    [Fact]
    public async Task AnalyzeAsync_FillsMissingMonthsWithZeros()
    {
        // Arrange: apenas um mês com dados (agosto 2026)
        _repository.GetMonthlyTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyAggregate>
            {
                new(2026, 8, 5000m, 3500m)
            });

        var analyzer = CreateAnalyzer();

        // Act: 3 meses (jun, jul, ago)
        var result = await analyzer.AnalyzeAsync(UserId, 3);

        // Assert: série contínua com junho/julho zerados
        result.Trend.Should().HaveCount(3);
        result.Trend[0].Month.Should().Be("2026-06");
        result.Trend[0].Income.Should().Be(0m);
        result.Trend[0].Expenses.Should().Be(0m);

        result.Trend[2].Month.Should().Be("2026-08");
        result.Trend[2].Income.Should().Be(5000m);
        result.Trend[2].Expenses.Should().Be(3500m);
        result.Trend[2].Balance.Should().Be(1500m);
    }

    [Fact]
    public async Task AnalyzeAsync_SumsTransactionsPerMonth()
    {
        // Arrange: vários meses
        _repository.GetMonthlyTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyAggregate>
            {
                new(2026, 7, 4000m, 3000m),
                new(2026, 8, 6000m, 4200m)
            });

        var analyzer = CreateAnalyzer();

        // Act: 12 meses
        var result = await analyzer.AnalyzeAsync(UserId, 12);

        // Assert
        result.Trend.Should().HaveCount(12);
        var jul = result.Trend.First(t => t.Month == "2026-07");
        jul.Income.Should().Be(4000m);
        jul.Balance.Should().Be(1000m);
    }

    [Fact]
    public async Task AnalyzeAsync_LastMonthIsCurrentMonth()
    {
        // Arrange
        _repository.GetMonthlyTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyAggregate>());
        var analyzer = CreateAnalyzer();

        // Act
        var result = await analyzer.AnalyzeAsync(UserId, 1);

        // Assert: o único ponto é o mês atual (agosto 2026)
        var expected = DateOnly.FromDateTime(DateTime.Today);
        result.Trend[0].Month.Should().Be($"{expected.Year:0000}-{expected.Month:00}");
    }
}
