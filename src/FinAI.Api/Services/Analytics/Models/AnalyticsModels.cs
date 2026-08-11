using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;

namespace FinAI.Api.Services.Analytics.Models;

public sealed record SpendingSummary(
    PeriodInfo Period,
    TotalsResult Totals,
    IReadOnlyList<CategorySpending> ByCategory,
    RecurringInfo Recurring,
    DateTimeOffset GeneratedAt);

public sealed record PeriodInfo(DateOnly From, DateOnly To);

public sealed record CategorySpending(
    string Category,
    string? Subcategory,
    decimal Amount,
    decimal Percentage);

public sealed record RecurringInfo(decimal Amount, decimal PercentageOfExpenses);

public sealed record BehaviorReport(
    IReadOnlyList<BehaviorInsight> Insights,
    PeriodInfo CurrentPeriod,
    PeriodInfo PreviousPeriod,
    DateTimeOffset GeneratedAt);

public sealed record BehaviorInsight(
    string Type,
    string? Category,
    string Metric,
    decimal? CurrentValue,
    decimal? PreviousValue,
    decimal? ChangePercent,
    decimal? Value,
    string Message);

public sealed record MonthlyTrendReport(
    IReadOnlyList<MonthlyTrendPoint> Trend,
    PeriodInfo Period,
    DateTimeOffset GeneratedAt);

public sealed record MonthlyTrendPoint(
    string Month,
    decimal Income,
    decimal Expenses,
    decimal Balance);

/// <summary>
/// Filtro de analytics — sempre exige UserId; AccountId opcional.
/// </summary>
public sealed record AnalyticsFilter(
    Guid UserId,
    DateOnly From,
    DateOnly To,
    Guid? AccountId = null);

/// <summary>
/// Dados brutos usados pelo BehaviorAnalyzer.
/// </summary>
public sealed record BehaviorData(
    IReadOnlyList<CategoryAggregate> CurrentByCategory,
    IReadOnlyList<CategoryAggregate> PreviousByCategory,
    decimal CurrentExpenses,
    decimal PreviousExpenses,
    decimal RecurringExpenses,
    decimal Income,
    decimal Expenses,
    int Months);
