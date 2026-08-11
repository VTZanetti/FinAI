using FinAI.Api.Repositories;
using FinAI.Api.Services.Analytics;
using FinAI.Api.Services.Analytics.Models;
using FluentAssertions;
using NSubstitute;

namespace FinAI.UnitTests.Services.Analytics;

[Trait("Category", "Unit")]
public class SpendingAnalyzerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IAnalyticsRepository _repository = Substitute.For<IAnalyticsRepository>();

    private SpendingAnalyzer CreateAnalyzer() => new(_repository);

    private static AnalyticsFilter Filter() => new(UserId, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 10));

    [Fact]
    public async Task AnalyzeAsync_ReturnsCorrectTotals()
    {
        // Arrange
        _repository.GetTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new TotalsResult(41600m, 27720m, 13880m));
        _repository.GetExpensesByCategoryAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<CategoryAggregate>
            {
                new("Food", "Restaurant", 4120.50m),
                new("Transportation", "Ride Sharing", 2000m),
                new(null, null, 21599.50m) // completa o total de despesas (27720)
            });
        _repository.GetRecurringExpensesAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(11642.40m);

        var analyzer = CreateAnalyzer();

        // Act
        var result = await analyzer.AnalyzeAsync(Filter());

        // Assert
        result.Totals.Income.Should().Be(41600m);
        result.Totals.Expenses.Should().Be(27720m);
        result.Totals.Balance.Should().Be(13880m);

        result.ByCategory.Should().Contain(c => c.Category == "Food" && c.Amount == 4120.50m);
        result.ByCategory.Should().Contain(c => c.Category == "Uncategorized" && c.Amount == 21599.50m);

        // Percentuais somam ~100%
        result.ByCategory.Sum(c => c.Percentage).Should().BeApproximately(100m, 0.5m);

        result.Recurring.Amount.Should().Be(11642.40m);
        result.Recurring.PercentageOfExpenses.Should().BeApproximately(42.0m, 0.5m);
    }

    [Fact]
    public async Task AnalyzeAsync_NoExpenses_ReturnsZeroPercentages()
    {
        // Arrange
        _repository.GetTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new TotalsResult(0m, 0m, 0m));
        _repository.GetExpensesByCategoryAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(new List<CategoryAggregate>());
        _repository.GetRecurringExpensesAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), null, Arg.Any<CancellationToken>())
            .Returns(0m);

        var analyzer = CreateAnalyzer();

        // Act
        var result = await analyzer.AnalyzeAsync(Filter());

        // Assert
        result.Totals.Balance.Should().Be(0m);
        result.ByCategory.Should().BeEmpty();
        result.Recurring.PercentageOfExpenses.Should().Be(0m);
    }

    [Fact]
    public async Task AnalyzeAsync_WithAccountFilter_PassesAccountId()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        _repository.GetTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), accountId, Arg.Any<CancellationToken>())
            .Returns(new TotalsResult(100m, 50m, 50m));
        _repository.GetExpensesByCategoryAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), accountId, Arg.Any<CancellationToken>())
            .Returns(new List<CategoryAggregate>());
        _repository.GetRecurringExpensesAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), accountId, Arg.Any<CancellationToken>())
            .Returns(0m);

        var analyzer = CreateAnalyzer();

        // Act
        var result = await analyzer.AnalyzeAsync(Filter() with { AccountId = accountId });

        // Assert
        result.Totals.Income.Should().Be(100m);
        await _repository.Received().GetTotalsAsync(UserId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), accountId, Arg.Any<CancellationToken>());
    }
}
