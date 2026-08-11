using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinAI.Api.Services.AI.External;

/// <summary>
/// Provider externo (OpenAI-compatible) — chama a API do provider com Bearer key lida em runtime.
/// </summary>
public class ExternalLlmProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalProviderConfig _config;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalLlmProvider> _logger;

    public ExternalLlmProvider(
        IHttpClientFactory httpClientFactory,
        ExternalProviderConfig config,
        IConfiguration configuration,
        ILogger<ExternalLlmProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => _config.Name;

    public bool IsAvailable => _config.Enabled && !string.IsNullOrWhiteSpace(GetApiKey()) && !string.IsNullOrWhiteSpace(_config.BaseUrl);

    public async Task<LlmChatResponse> CompleteChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return new LlmChatResponse(string.Empty, false, $"External provider '{_config.Name}' is not available");

        var client = _httpClientFactory.CreateClient("external-llm");
        client.Timeout = TimeSpan.FromSeconds(60);
        client.BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/'));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetApiKey());

        var body = new
        {
            model = _config.Model,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserMessage }
            },
            temperature = request.Temperature
        };

        try
        {
            var response = await client.PostAsJsonAsync("/chat/completions", body, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new LlmChatResponse(string.Empty, false, $"External provider returned {(int)response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(cancellationToken);
            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

            return new LlmChatResponse(content, !string.IsNullOrWhiteSpace(content), null,
                result?.Usage?.PromptTokens ?? 0, result?.Usage?.CompletionTokens ?? 0);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LlmChatResponse(string.Empty, false, "External provider timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "External provider {Name} failed: {Message}", _config.Name, ex.Message);
            return new LlmChatResponse(string.Empty, false, "External provider unavailable");
        }
    }

    private string? GetApiKey()
        => _configuration[$"Ai:ExternalProviders:{_config.Name}:ApiKey"]
           ?? Environment.GetEnvironmentVariable(_config.ApiKeyEnvVar)
           ?? _configuration[_config.ApiKeyEnvVar];

    private sealed class ChatCompletionsResponse
    {
        public List<Choice>? Choices { get; set; }
        public UsageInfo? Usage { get; set; }
    }

    private sealed class Choice
    {
        public MessageInfo? Message { get; set; }
    }

    private sealed class MessageInfo
    {
        public string? Content { get; set; }
    }

    private sealed class UsageInfo
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}
