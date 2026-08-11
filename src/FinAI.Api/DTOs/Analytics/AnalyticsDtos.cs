using FinAI.Api.Services.Analytics.Models;

namespace FinAI.Api.DTOs.Analytics;

public sealed record SpendingSummaryResponse(
    PeriodInfoDto Period,
    TotalsDto Totals,
    IReadOnlyList<CategorySpendingDto> ByCategory,
    RecurringInfoDto Recurring,
    DateTimeOffset GeneratedAt);

public sealed record PeriodInfoDto(DateOnly From, DateOnly To);

public sealed record TotalsDto(decimal Income, decimal Expenses, decimal Balance);

public sealed record CategorySpendingDto(string Category, string? Subcategory, decimal Amount, decimal Percentage);

public sealed record RecurringInfoDto(decimal Amount, decimal PercentageOfExpenses);

public sealed record BehaviorReportResponse(
    IReadOnlyList<BehaviorInsightDto> Insights,
    PeriodInfoDto CurrentPeriod,
    PeriodInfoDto PreviousPeriod,
    DateTimeOffset GeneratedAt);

public sealed record BehaviorInsightDto(
    string Type,
    string? Category,
    string Metric,
    decimal? CurrentValue,
    decimal? PreviousValue,
    decimal? ChangePercent,
    decimal? Value,
    string Message);

public sealed record MonthlyTrendReportResponse(
    IReadOnlyList<MonthlyTrendPointDto> Trend,
    PeriodInfoDto Period,
    DateTimeOffset GeneratedAt);

public sealed record MonthlyTrendPointDto(string Month, decimal Income, decimal Expenses, decimal Balance);

public static class AnalyticsMappings
{
    public static PeriodInfoDto ToDto(this PeriodInfo p) => new(p.From, p.To);

    public static SpendingSummaryResponse ToResponse(this SpendingSummary s)
        => new(
            s.Period.ToDto(),
            new TotalsDto(s.Totals.Income, s.Totals.Expenses, s.Totals.Balance),
            s.ByCategory.Select(c => new CategorySpendingDto(c.Category, c.Subcategory, c.Amount, c.Percentage)).ToList(),
            new RecurringInfoDto(s.Recurring.Amount, s.Recurring.PercentageOfExpenses),
            s.GeneratedAt);

    public static BehaviorReportResponse ToResponse(this BehaviorReport r)
        => new(
            r.Insights.Select(i => new BehaviorInsightDto(i.Type, i.Category, i.Metric, i.CurrentValue, i.PreviousValue, i.ChangePercent, i.Value, i.Message)).ToList(),
            r.CurrentPeriod.ToDto(),
            r.PreviousPeriod.ToDto(),
            r.GeneratedAt);

    public static MonthlyTrendReportResponse ToResponse(this MonthlyTrendReport r)
        => new(
            r.Trend.Select(t => new MonthlyTrendPointDto(t.Month, t.Income, t.Expenses, t.Balance)).ToList(),
            r.Period.ToDto(),
            r.GeneratedAt);
}
