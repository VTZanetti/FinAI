namespace FinAI.Api.Services.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "llama3.2";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int TimeoutSeconds { get; set; } = 60;
    public int RetryCount { get; set; } = 1;
    public bool ClassificationCacheEnabled { get; set; } = true;
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Abstração de provider de LLM (ADR-004). Implementação padrão: Ollama local.
/// Providers externos entram na v0.7 via endpoints custom.
/// </summary>
public interface ILlmProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    Task<LlmChatResponse> CompleteChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}

public sealed record LlmChatRequest(string SystemPrompt, string UserMessage, double Temperature = 0.2);

public sealed record LlmChatResponse(string Content, bool Success, string? Error = null, int PromptTokens = 0, int CompletionTokens = 0);
