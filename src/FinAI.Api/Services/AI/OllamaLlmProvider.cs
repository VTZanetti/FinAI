using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.AI;

/// <summary>
/// Provider padrão: Ollama local (API /api/chat) — ADR-004.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmOptions _options;
    private readonly ILogger<OllamaLlmProvider> _logger;

    public OllamaLlmProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmOptions> options,
        ILogger<OllamaLlmProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "ollama";

    public bool IsAvailable => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<LlmChatResponse> CompleteChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return new LlmChatResponse(string.Empty, false, "LLM provider is disabled");

        var client = _httpClientFactory.CreateClient("ollama");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var body = new
        {
            model = _options.ChatModel,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserMessage }
            },
            options = new { temperature = request.Temperature }
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/chat", body, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Ollama returned {Status} on chat: {Body}", response.StatusCode, Truncate(errorBody));
                return new LlmChatResponse(string.Empty, false, $"Ollama returned {(int)response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
            var content = result?.Message?.Content ?? string.Empty;

            return new LlmChatResponse(
                content,
                !string.IsNullOrWhiteSpace(content),
                null,
                result?.PromptEvalCount ?? 0,
                result?.EvalCount ?? 0);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Ollama chat timed out after {Timeout}s", _options.TimeoutSeconds);
            return new LlmChatResponse(string.Empty, false, "LLM timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama chat failed: {Message}", ex.Message);
            return new LlmChatResponse(string.Empty, false, "LLM unavailable");
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];

    private sealed class OllamaChatResponse
    {
        public OllamaMessage? Message { get; set; }
        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }
        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string? Content { get; set; }
    }
}
