using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace FinAI.Api.Services.Caching;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default);
    void Remove(string key);
    void RemoveByPrefix(string prefix);
}

/// <summary>
/// Cache com fallback: usa IMemoryCache sempre; integra Redis quando disponível (v1.0).
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;

    // Rastreia as chaves criadas para permitir invalidação por prefixo
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var value = await factory();
        if (value is not null)
        {
            _keys.TryAdd(key, 0);
            _cache.Set(key, value, ttl);
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