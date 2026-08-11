using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.AI;

/// <summary>
/// Wrapper de chat com retries/backoff e métricas simples.
/// </summary>
public interface IChatService
{
    Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}

public class ChatService : IChatService
{
    private readonly ILlmProvider _provider;
    private readonly LlmOptions _options;
    private readonly ILogger<ChatService> _logger;

    public ChatService(ILlmProvider provider, IOptions<LlmOptions> options, ILogger<ChatService> logger)
    {
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmChatResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        var retries = Math.Max(0, _options.RetryCount);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            var response = await _provider.CompleteChatAsync(request, cancellationToken);
            if (response.Success)
                return response;

            if (attempt < retries)
            {
                var backoff = TimeSpan.FromMilliseconds(250 * (attempt + 1));
                _logger.LogWarning("LLM attempt {Attempt}/{Total} failed: {Error}; retrying in {Backoff}ms",
                    attempt + 1, retries + 1, response.Error, backoff.TotalMilliseconds);
                await Task.Delay(backoff, cancellationToken);
            }
        }

        // Última tentativa (sem retry) retorna o erro controlado
        var last = await _provider.CompleteChatAsync(request, cancellationToken);
        return last;
    }
}
