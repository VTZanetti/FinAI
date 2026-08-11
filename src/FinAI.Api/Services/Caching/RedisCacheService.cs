using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace FinAI.Api.Services.Caching;

/// <summary>
/// Implementação de ICacheService sobre Redis (IDistributedCache).
/// Usa JSON para serialização e um prefixo para invalidação por prefixo
/// via SCAN/KEYS — aqui simplificado com rastreio local das chaves.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    // Rastreia as chaves criadas nesta instância para permitir invalidação por prefixo
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _keys = new();

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var raw = await _cache.GetStringAsync(key, cancellationToken);
        if (raw is not null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(raw);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Falha ao desserializar cache Redis para {Key}", key);
                await _cache.RemoveAsync(key, cancellationToken);
            }
        }

        var value = await factory();
        if (value is not null)
        {
            _keys.TryAdd(key, 0);
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, cancellationToken);
        }

        return value;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }
    }
}
