using FinAI.Api.Common;
using FinAI.Api.Models.Enums;
using FinAI.Api.Repositories;
using FinAI.Api.Services.AnomalyDetection.Models;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.AnomalyDetection;

public interface IAnomalyDetectionService
{
    Task<Result<AnomalyDetectionReport>> DetectAsync(Guid userId, DateOnly from, DateOnly to, string? method = null, Guid? accountId = null, CancellationToken cancellationToken = default);
    Task<Result<AnomalyCheckResult>> CheckAsync(Guid userId, string description, decimal amount, Guid? categoryId, CancellationToken cancellationToken = default);
}

public sealed record AnomalyDetectionReport(string Method, IReadOnlyList<AnomalyResult> Items);

/// <summary>
/// Detecção de anomalias (FR-08): Z-score (padrão) ou IQR, com mínimo de amostras.
/// Transações de despesa agrupadas por categoria; histórico = últimos 12 meses.
/// </summary>
public class AnomalyDetectionService : IAnomalyDetectionService
{
    private const int HistoryMonths = 12;
    private const string NoCategory = "Uncategorized";

    private readonly ITransactionRepository _transactions;
    private readonly IAnomalyDetector _zscore;
    private readonly IAnomalyDetector _iqr;
    private readonly ILogger<AnomalyDetectionService> _logger;

    public AnomalyDetectionService(
        ITransactionRepository transactions,
        IOptions<AnomalyDetectionOptions> options,
        ILogger<AnomalyDetectionService> logger)
    {
        _transactions = transactions;
        _logger = logger;
        _zscore = new ZScoreAnomalyDetector(options.Value.MinSamplesForZScore, options.Value.AnomalyZScoreThreshold);
        _iqr = new IqrAnomalyDetector(options.Value.MinSamplesForIqr);
    }

    public async Task<Result<AnomalyDetectionReport>> DetectAsync(Guid userId, DateOnly from, DateOnly to, string? method = null, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        if (from > to)
            return Result.Failure<AnomalyDetectionReport>(ErrorCode.Validation, "from must be before or equal to to");

        var historyFrom = new DateOnly(from.Year, from.Month, 1).AddMonths(-(HistoryMonths - 1));

        // Transações de despesa do período (alvo)
        var periodFilter = new TransactionFilter(
            AccountId: accountId,
            Type: TransactionType.Expense,
            From: from,
            To: to,
            PageSize: int.MaxValue);
        var period = await _transactions.QueryAsync(userId, periodFilter, cancellationToken);

        // Histórico completo por categoria (para o baseline)
        var historyFilter = new TransactionFilter(
            AccountId: accountId,
            Type: TransactionType.Expense,
            From: historyFrom,
            To: to,
            PageSize: int.MaxValue);
        var history = await _transactions.QueryAsync(userId, historyFilter, cancellationToken);

        var historyByCategory = history.Items
            .GroupBy(t => t.Category?.Name ?? NoCategory)
            .ToDictionary(g => g.Key, g => g.Select(t => Math.Abs(t.Amount)).ToList());
        var useIqr = string.Equals(method, "iqr", StringComparison.OrdinalIgnoreCase);
        var detector = useIqr ? _iqr : _zscore;

        var items = new List<AnomalyResult>();
        foreach (var transaction in period.Items)
        {
            var category = transaction.Category?.Name ?? NoCategory;
            historyByCategory.TryGetValue(category, out var baseline);

            var assessment = detector.Assess(Math.Abs(transaction.Amount), baseline ?? []);
            if (assessment.Anomaly)
                Telemetry.FinAiMetrics.RecordAnomaly();

            items.Add(new AnomalyResult(
                transaction.Id,
                transaction.Description,
                transaction.Amount,
                transaction.Date,
                category,
                assessment.Anomaly,
                assessment.Score,
                assessment.Reason,
                assessment.Method));
        }

        return Result.Success(new AnomalyDetectionReport(detector.MethodName, items));
    }

    public async Task<Result<AnomalyCheckResult>> CheckAsync(Guid userId, string description, decimal amount, Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var historyFrom = new DateOnly(today.Year, today.Month, 1).AddMonths(-(HistoryMonths - 1));

        var history = await _transactions.QueryAsync(userId, new TransactionFilter(
            CategoryId: categoryId,
            Type: TransactionType.Expense,
            From: historyFrom,
            To: today,
            PageSize: int.MaxValue), cancellationToken);

        var values = history.Items.Select(t => Math.Abs(t.Amount)).ToList();

        // Escolha do método: Z-score se amostras suficientes, senão IQR
        var detector = _zscore.HasEnoughSamples(values.Count) ? _zscore : _iqr;
        var assessment = detector.Assess(Math.Abs(amount), values);

        return Result.Success(new AnomalyCheckResult(
            assessment.Anomaly,
            assessment.Score,
            assessment.Reason,
            assessment.Anomaly ? "review" : "ok",
            assessment.Method));
    }
}
