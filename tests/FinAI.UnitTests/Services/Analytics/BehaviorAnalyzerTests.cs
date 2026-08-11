using FinAI.Api.Services.Analytics;
using FinAI.Api.Services.Analytics.Models;
using FluentAssertions;
using FinAI.Api.Repositories;

namespace FinAI.UnitTests.Services.Analytics;

[Trait("Category", "Unit")]
public class BehaviorAnalyzerTests
{
    private static CategoryAggregate Cat(string? name, string? sub, decimal amount) => new(name, sub, amount);

    private static BehaviorData DataWith(
        IReadOnlyList<CategoryAggregate> current,
        IReadOnlyList<CategoryAggregate> previous,
        decimal currentExpenses,
        decimal previousExpenses,
        decimal recurring = 0m,
        decimal income = 0m)
        => new(current, previous, currentExpenses, previousExpenses, recurring, income, currentExpenses, 3);

    [Fact]
    public void CalculateChangePercent_Increase_ReturnsPositive()
    {
        // Act
        var result = BehaviorAnalyzer.CalculateChangePercent(4120.50m, 3350.00m);

        // Assert: (4120.5 - 3350) / 3350 * 100 ≈ 23.0
        result.Should().BeApproximately(23.0m, 0.5m);
    }

    [Fact]
    public void CalculateChangePercent_ZeroPrevious_Returns100WhenCurrentPositive()
    {
        // Act
        var result = BehaviorAnalyzer.CalculateChangePercent(500m, 0m);

        // Assert
        result.Should().Be(100m);
    }

    [Fact]
    public void CalculateChangePercent_ZeroBoth_ReturnsZero()
    {
        // Act
        var result = BehaviorAnalyzer.CalculateChangePercent(0m, 0m);

        // Assert
        result.Should().Be(0m);
    }

    [Fact]
    public void BuildInsights_CategoryIncreaseAboveThreshold_AddsInsight()
    {
        // Arrange
        var data = DataWith(
            [Cat("Food", "Restaurant", 4120.50m)],
            [Cat("Food", "Restaurant", 3350.00m)],
            4120.50m,
            3350.00m);

        // Act
        var insights = BehaviorAnalyzer.BuildInsights(data);

        // Assert
        var insight = insights.First(i => i.Type == "category_increase");
        insight.Category.Should().Be("Food");
        insight.ChangePercent.Should().BeApproximately(23.0m, 0.5m);
        insight.Message.Should().Contain("aumentaram");
    }

    [Fact]
    public void BuildInsights_SmallVariation_BelowThreshold_NoCategoryInsight()
    {
        // Arrange: variação de 5% (< 10%)
        var data = DataWith(
            [Cat("Food", null, 1050m)],
            [Cat("Food", null, 1000m)],
            1050m,
            1000m);

        // Act
        var insights = BehaviorAnalyzer.BuildInsights(data);

        // Assert
        insights.Should().NotContain(i => i.Type == "category_increase");
        insights.Should().NotContain(i => i.Type == "category_decrease");
    }

    [Fact]
    public void BuildInsights_RecurringRatio_AddsInsight()
    {
        // Arrange
        var data = DataWith([Cat("Housing", "Rent", 1000m)], [], 1000m, 0m, recurring: 420m, income: 2000m);

        // Act
        var insights = BehaviorAnalyzer.BuildInsights(data);

        // Assert
        var recurring = insights.First(i => i.Type == "recurring_ratio");
        recurring.Value.Should().Be(42m); // 420/1000
        recurring.Message.Should().Contain("42.00%");
    }

    [Fact]
    public void BuildInsights_IncomeExpenseHealthy_WhenIncomeCoversExpenses()
    {
        // Arrange
        var data = DataWith([Cat("Food", null, 500m)], [], 500m, 0m, income: 1000m);

        // Act
        var insights = BehaviorAnalyzer.BuildInsights(data);

        // Assert
        insights.Should().Contain(i => i.Type == "income_expense_healthy");
    }

    [Fact]
    public void BuildInsights_IncomeExpenseRisk_WhenExpensesExceedIncome()
    {
        // Arrange
        var data = DataWith([Cat("Food", null, 1500m)], [], 1500m, 0m, income: 1000m);

        // Act
        var insights = BehaviorAnalyzer.BuildInsights(data);

        // Assert
        insights.Should().Contain(i => i.Type == "income_expense_risk");
    }

    [Fact]
    public void BuildInsights_TopCategory_IsLargestSpending()
    {
        // Arrange
        var data = DataWith(
            [Cat("Food", null, 1000m), Cat("Travel", null, 3000m), Cat("Shopping", null, 500m)],
            [],
            4500m,
            0m);

        // Act
        var insights = BehaviorAnalyzer.BuildInsights(data);

        // Assert
        var top = insights.First(i => i.Type == "top_category");
        top.Category.Should().Be("Travel");
        top.Value.Should().BeApproximately(66.67m, 0.5m); // 3000/4500
    }
}
