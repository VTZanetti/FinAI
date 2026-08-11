using System.Collections.Concurrent;
using System.Text.Json;

namespace FinAI.Api.Services.AI.External;

/// <summary>
/// Registro em memória de providers externos. Chaves de API nunca são persistidas —
/// apenas o nome da variável de ambiente. (Persistência em banco fica para v1.0.)
/// </summary>
public interface IExternalProviderRegistry
{
    ExternalProviderConfig? Get(string name);
    IReadOnlyList<ExternalProviderConfig> List();
    void Upsert(ExternalProviderConfig config);
    bool Remove(string name);
}

public class ExternalProviderRegistry : IExternalProviderRegistry
{
    private readonly ConcurrentDictionary<string, ExternalProviderConfig> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ExternalProviderConfig? Get(string name)
        => _providers.TryGetValue(name, out var config) ? config : null;

    public IReadOnlyList<ExternalProviderConfig> List()
        => _providers.Values.OrderBy(p => p.Name).ToList();

    public void Upsert(ExternalProviderConfig config)
        => _providers[config.Name] = config;

    public bool Remove(string name)
        => _providers.TryRemove(name, out _);
}

public static class ExternalProviderRegistryExtensions
{
    /// <summary>Serializa sem expor chaves (apenas o nome da env var).</summary>
    public static object ToSafeDto(this ExternalProviderConfig config)
        => new
        {
            name = config.Name,
            type = config.Type.ToString(),
            baseUrl = config.BaseUrl,
            model = config.Model,
            apiKeyEnvVar = config.ApiKeyEnvVar,
            enabled = config.Enabled
        };
}
