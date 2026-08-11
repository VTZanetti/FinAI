using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.AI;

public sealed record EmbeddingResult(float[] Values, bool Success, string? Error = null);

/// <summary>
/// Gera embeddings de texto — padrão: Ollama local (/api/embed, modelo nomic-embed-text).
/// </summary>
public interface IEmbeddingService
{
    bool IsAvailable { get; }
    Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default);
    Task<EmbeddingResult> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmOptions _options;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmOptions> options,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsAvailable => _options.Enabled && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => EmbedBatchAsync([text], cancellationToken);

    public async Task<EmbeddingResult> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return new EmbeddingResult([], false, "Embedding provider is disabled");

        var client = _httpClientFactory.CreateClient("ollama");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var body = new
        {
            model = _options.EmbeddingModel,
            input = texts
        };

        try
        {
            var response = await client.PostAsJsonAsync("/api/embed", body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama embed returned {Status}", response.StatusCode);
                return new EmbeddingResult([], false, $"Ollama returned {(int)response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken);
            if (result?.Embeddings is null || result.Embeddings.Count == 0)
                return new EmbeddingResult([], false, "No embeddings returned");

            return new EmbeddingResult(result.Embeddings[0], true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Ollama embed timed out after {Timeout}s", _options.TimeoutSeconds);
            return new EmbeddingResult([], false, "Embedding timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama embed failed: {Message}", ex.Message);
            return new EmbeddingResult([], false, "Embedding provider unavailable");
        }
    }

    private sealed class OllamaEmbedResponse
    {
        public List<float[]>? Embeddings { get; set; }
    }
}
