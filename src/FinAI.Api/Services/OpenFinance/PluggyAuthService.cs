using Microsoft.Extensions.Caching.Memory;

namespace FinAI.Api.Services.OpenFinance;

public interface IPluggyAuthService
{
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Autenticação na Pluggy com cache em memória da apiKey (expira ~30 min conforme expiresAt).
/// A chave nunca é persistida.
/// </summary>
public class PluggyAuthService : IPluggyAuthService
{
    private const string CacheKey = "pluggy:api-key";

    private readonly IPluggyClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PluggyAuthService> _logger;

    public PluggyAuthService(IPluggyClient client, IMemoryCache cache, ILogger<PluggyAuthService> logger)
    {
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
            return cached;

        var auth = await _client.AuthenticateAsync(cancellationToken);

        // TTL = expiresAt - now - margem (60s)
        var ttl = TimeSpan.FromSeconds(Math.Max(60, auth.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60));

        _cache.Set(CacheKey, auth.AccessToken, ttl);
        _logger.LogInformation("Pluggy apiKey cached for {Ttl} seconds", ttl.TotalSeconds);

        return auth.AccessToken;
    }
}