using FinAI.Api.Common;
using FinAI.Api.Services.Analytics;

namespace FinAI.Api.Services.AI;

public sealed record AdvisorRequest(string Question);

public sealed record AdvisorResponse(string Answer, object Context, IReadOnlyList<string> Sources);

public interface IFinancialAdvisorService
{
    Task<Result<AdvisorResponse>> AskAsync(Guid userId, string question, CancellationToken cancellationToken = default);
}

/// <summary>
/// Assistente financeiro: contexto real do usuário (transações + analytics) → LLM → resposta com fonte.
/// Fluxo: contexto primeiro, LLM depois (02-arquitetura.md §4.2).
/// </summary>
public class FinancialAdvisorService : IFinancialAdvisorService
{
    private const int MaxQuestionLength = 500;

    private readonly IChatService _chat;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IAnalyticsService _analytics;
    private readonly ILogger<FinancialAdvisorService> _logger;

    public FinancialAdvisorService(
        IChatService chat,
        IPromptBuilder promptBuilder,
        IAnalyticsService analytics,
        ILogger<FinancialAdvisorService> logger)
    {
        _chat = chat;
        _promptBuilder = promptBuilder;
        _analytics = analytics;
        _logger = logger;
    }

    public async Task<Result<AdvisorResponse>> AskAsync(Guid userId, string question, CancellationToken cancellationToken = default)
    {
        var sanitized = (question ?? string.Empty).Trim();
        if (sanitized.Length == 0)
            return Result.Failure<AdvisorResponse>(ErrorCode.Validation, "Question is required");
        if (sanitized.Length > MaxQuestionLength)
            return Result.Failure<AdvisorResponse>(ErrorCode.Validation, $"Question must be at most {MaxQuestionLength} characters");

        // 1. Período relevante (heurística simples; fallback: últimos 3 meses)
        var (from, to) = ResolvePeriod(sanitized);
        var months = EstimateMonths(from, to);

        // 2. Dados reais do usuário (analytics da v0.3)
        var summaryResult = await _analytics.GetSpendingSummaryAsync(userId, from, to, cancellationToken: cancellationToken);
        if (!summaryResult.IsSuccess)
            return Result.Failure<AdvisorResponse>(summaryResult.Error, summaryResult.Message);

        var behaviorResult = await _analytics.GetBehaviorAsync(userId, months, cancellationToken: cancellationToken);
        var trendResult = await _analytics.GetMonthlyTrendAsync(userId, Math.Min(months, 12), cancellationToken: cancellationToken);

        // 3. Contexto estruturado (JSON)
        var context = new
        {
            period = new { from, to },
            totals = summaryResult.Value!.Totals,
            byCategory = summaryResult.Value.ByCategory,
            recurring = summaryResult.Value.Recurring,
            behaviorInsights = behaviorResult.IsSuccess ? behaviorResult.Value!.Insights : null,
            monthlyTrend = trendResult.IsSuccess ? trendResult.Value!.Trend : null
        };

        // 4. LLM com prompt anti-alucinação
        var prompt = _promptBuilder.BuildAdvisorPrompt(sanitized, context);
        var response = await _chat.ChatAsync(new LlmChatRequest(prompt.SystemPrompt, prompt.UserMessage), cancellationToken);

        if (!response.Success)
            return Result.Failure<AdvisorResponse>(ErrorCode.Internal,
                "The AI assistant is temporarily unavailable. Context data was calculated — try again shortly.");

        var answer = response.Content.Trim();

        // 5. Sanitização básica: rejeitar respostas que exponham o system prompt
        if (IsPromptLeak(response.Content))
        {
            _logger.LogWarning("Advisor response may contain prompt leakage");
            return Result.Failure<AdvisorResponse>(ErrorCode.Internal, "Could not generate a safe response.");
        }

        return Result.Success(new AdvisorResponse(answer, context, ["analytics"]));
    }

    internal static (DateOnly From, DateOnly To) ResolvePeriod(string question)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var q = question.ToLowerInvariant();

        if (q.Contains("mês passado") || q.Contains("mes passado") || q.Contains("último mês") || q.Contains("ultimo mes"))
            return (new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, 1).AddDays(-1));

        if (q.Contains("este mês") || q.Contains("este mes") || q.Contains("esse mês") || q.Contains("esse mes"))
            return (new DateOnly(today.Year, today.Month, 1), today);

        if (q.Contains("3 meses") || q.Contains("trimestre"))
            return (new DateOnly(today.Year, today.Month, 1).AddMonths(-2), today);

        if (q.Contains("6 meses"))
            return (new DateOnly(today.Year, today.Month, 1).AddMonths(-5), today);

        if (q.Contains("12 meses") || q.Contains("ano"))
            return (new DateOnly(today.Year, today.Month, 1).AddMonths(-11), today);

        // Fallback: últimos 3 meses
        return (new DateOnly(today.Year, today.Month, 1).AddMonths(-2), today);
    }

    internal static int EstimateMonths(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + (to.Month - from.Month) + 1;
        return Math.Clamp(months, 1, 24);
    }

    internal static bool IsPromptLeak(string content)
    {
        var lower = content.ToLowerInvariant();
        return lower.Contains("ignore qualquer instrução", StringComparison.Ordinal)
            || lower.Contains("sistema: você é", StringComparison.Ordinal)
            || lower.Contains("system prompt", StringComparison.Ordinal);
    }
}
