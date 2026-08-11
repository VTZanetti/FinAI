namespace FinAI.Api.Services.AI.External;

public interface IExternalLlmProviderFactory
{
    ILlmProvider? Create(string providerName);
}

/// <summary>
/// Resolve um provider externo registrado por nome (nulo se não existir).
/// </summary>
public class ExternalLlmProviderFactory : IExternalLlmProviderFactory
{
    private readonly IExternalProviderRegistry _registry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public ExternalLlmProviderFactory(
        IExternalProviderRegistry registry,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _registry = registry;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    public ILlmProvider? Create(string providerName)
    {
        var config = _registry.Get(providerName);
        if (config is null)
            return null;

        return new ExternalLlmProvider(
            _httpClientFactory,
            config,
            _configuration,
            _loggerFactory.CreateLogger<ExternalLlmProvider>());
    }
}
