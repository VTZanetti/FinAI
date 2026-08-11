using System.Text.Json;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.AI;

public interface IClassificationService
{
    Task<ClassificationResult> ClassifyAsync(Guid userId, string description, decimal amount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Classificação em cascata (ADR-005): rules → cached → llm → fallback(melhor anterior).
/// Sempre retorna source: rules | cached | llm | fallback.
/// </summary>
public class ClassificationService : IClassificationService
{
    private const decimal CacheWriteThreshold = 0.8m;
    private const decimal FallbackConfidence = 0.4m;
    private const int MaxDescriptionLength = 200;

    private readonly IRuleClassifier _rules;
    private readonly IClassificationCacheRepository _cache;
    private readonly IChatService _chat;
    private readonly IPromptBuilder _promptBuilder;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly LlmOptions _options;
    private readonly ILogger<ClassificationService> _logger;

    public ClassificationService(
        IRuleClassifier rules,
        IClassificationCacheRepository cache,
        IChatService chat,
        IPromptBuilder promptBuilder,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IOptions<LlmOptions> options,
        ILogger<ClassificationService> logger)
    {
        _rules = rules;
        _cache = cache;
        _chat = chat;
        _promptBuilder = promptBuilder;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(Guid userId, string description, decimal amount, CancellationToken cancellationToken = default)
    {
        var sanitized = SanitizeDescription(description);
        var normalized = TextNormalizer.Normalize(sanitized);
        var amountBucket = GetAmountBucket(amount);

        // 1. Regras (sem LLM)
        var ruleResult = _rules.Match(sanitized);
        if (ruleResult is not null)
        {
            Telemetry.FinAiMetrics.RecordClassification("rules");
            return await ResolveCategoryIdAsync(userId, ruleResult, cancellationToken);
        }

        // 2. Cache de exemplos aprendidos
        if (_options.ClassificationCacheEnabled)
        {
            var cached = await _cache.FindSimilarAsync(userId, normalized, amountBucket, cancellationToken);
            if (cached is not null)
            {
                cached.HitCount++;
                cached.LastUsedAt = DateTimeOffset.UtcNow;
                _cache.Update(cached);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var category = await _categories.GetByIdAsync(cached.CategoryId, userId, cancellationToken);
                if (category is not null)
                {
                    Telemetry.FinAiMetrics.RecordClassification("cached");
                    return new ClassificationResult(category.Id, category.Name, category.Subcategory, cached.Confidence, "cached");
                }
            }
        }

        // 3. LLM
        var allowedCategories = await _categories.ListForUserAsync(userId, cancellationToken: cancellationToken);
        if (allowedCategories.Count > 0)
        {
            var prompt = _promptBuilder.BuildClassificationPrompt(sanitized, amount, allowedCategories);
            var response = await _chat.ChatAsync(new LlmChatRequest(prompt.SystemPrompt, prompt.UserMessage), cancellationToken);

            if (response.Success)
            {
                var parsed = TryParseLlmResponse(response.Content);
                if (parsed is not null)
                {
                    var matched = FindCategory(allowedCategories, parsed.Category, parsed.Subcategory);
                    if (matched is not null)
                    {
                        // 4. Grava cache se confiança alta
                        if (parsed.Confidence >= CacheWriteThreshold)
                        {
                            await SaveToCacheAsync(userId, normalized, amountBucket, matched, parsed.Confidence, cancellationToken);
                        }

                        Telemetry.FinAiMetrics.RecordClassification("llm");
                        return new ClassificationResult(matched.Id, matched.Name, matched.Subcategory, parsed.Confidence, "llm");
                    }
                }
                else
                {
                    _logger.LogWarning("LLM classification response could not be parsed: {Response}", Truncate(response.Content));
                }
            }
        }

        // 5. Fallback: categoria "Other" com confiança baixa
        Telemetry.FinAiMetrics.RecordClassification("fallback");
        var other = allowedCategories.FirstOrDefault(c => c.Name == "Other" && c.IsSystem);
        return new ClassificationResult(
            other?.Id,
            other?.Name ?? "Other",
            other?.Subcategory,
            FallbackConfidence,
            "fallback");
    }

    /// <summary>
    /// Resolve o CategoryId real da categoria do sistema quando o resultado não o carrega (ex.: regras).
    /// </summary>
    private async Task<ClassificationResult> ResolveCategoryIdAsync(Guid userId, ClassificationResult result, CancellationToken cancellationToken)
    {
        if (result.CategoryId.HasValue)
            return result;

        var categories = await _categories.ListForUserAsync(userId, cancellationToken: cancellationToken);
        var match = FindCategory(categories, result.Category, result.Subcategory);
        if (match is null)
            return result;

        return result with { CategoryId = match.Id };
    }

    private async Task SaveToCacheAsync(Guid userId, string normalized, string amountBucket, Category category, decimal confidence, CancellationToken cancellationToken)
    {
        var entry = new ClassificationCache
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NormalizedDescription = normalized.Length > 255 ? normalized[..255] : normalized,
            AmountBucket = amountBucket,
            CategoryId = category.Id,
            Confidence = confidence,
            HitCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            LastUsedAt = DateTimeOffset.UtcNow
        };

        await _cache.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static Category? FindCategory(IReadOnlyList<Category> categories, string? name, string? subcategory)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return categories.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(subcategory) || (c.Subcategory ?? string.Empty).Equals(subcategory, StringComparison.OrdinalIgnoreCase)))
            ?? categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static LlmParsedResult? TryParseLlmResponse(string content)
    {
        try
        {
            // Extrai o JSON da resposta (pode vir com texto ao redor)
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start)
                return null;

            var json = content[start..(end + 1)];
            var parsed = JsonSerializer.Deserialize<LlmParsedResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Category))
                return null;

            parsed.Confidence = Math.Clamp(parsed.Confidence, 0m, 1m);
            return parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SanitizeDescription(string description)
    {
        var trimmed = (description ?? string.Empty).Trim();
        return trimmed.Length > MaxDescriptionLength ? trimmed[..MaxDescriptionLength] : trimmed;
    }

    private static string GetAmountBucket(decimal amount)
    {
        var abs = Math.Abs(amount);
        return abs switch
        {
            < 50m => "lt50",
            < 200m => "lt200",
            < 1000m => "lt1000",
            _ => "gte1000"
        };
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];

    private sealed class LlmParsedResult
    {
        public string? Category { get; set; }
        public string? Subcategory { get; set; }
        public decimal Confidence { get; set; }
    }
}
