using FinAI.Api.Common;
using FinAI.Api.Services.Analytics.Models;

namespace FinAI.Api.Services.Analytics;

public interface IAnalyticsService
{
    Task<Result<SpendingSummary>> GetSpendingSummaryAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId = null, CancellationToken cancellationToken = default);
    Task<Result<BehaviorReport>> GetBehaviorAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default);
    Task<Result<MonthlyTrendReport>> GetMonthlyTrendAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default);
}

public class AnalyticsService : IAnalyticsService
{
    private const int MaxBehaviorMonths = 12;
    private const int MaxTrendMonths = 24;

    private readonly ISpendingAnalyzer _spending;
    private readonly IBehaviorAnalyzer _behavior;
    private readonly IMonthlyTrendAnalyzer _trend;

    public AnalyticsService(ISpendingAnalyzer spending, IBehaviorAnalyzer behavior, IMonthlyTrendAnalyzer trend)
    {
        _spending = spending;
        _behavior = behavior;
        _trend = trend;
    }

    public async Task<Result<SpendingSummary>> GetSpendingSummaryAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return Result.Failure<SpendingSummary>(ErrorCode.Validation, "from must be before or equal to to");

        var summary = await _spending.AnalyzeAsync(new AnalyticsFilter(userId, from, to, accountId), cancellationToken);
        return Result.Success(summary);
    }

    public async Task<Result<BehaviorReport>> GetBehaviorAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > MaxBehaviorMonths)
            return Result.Failure<BehaviorReport>(ErrorCode.Validation, $"months must be between 1 and {MaxBehaviorMonths}");

        var report = await _behavior.AnalyzeAsync(userId, months, accountId, cancellationToken);
        return Result.Success(report);
    }

    public async Task<Result<MonthlyTrendReport>> GetMonthlyTrendAsync(Guid userId, int months, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > MaxTrendMonths)
            return Result.Failure<MonthlyTrendReport>(ErrorCode.Validation, $"months must be between 1 and {MaxTrendMonths}");

        var report = await _trend.AnalyzeAsync(userId, months, accountId, cancellationToken);
        return Result.Success(report);
    }
}
