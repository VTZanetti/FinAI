namespace FinAI.Api.Services.AI.External;

public enum ExternalProviderType
{
    OpenAI = 1,
    AzureOpenAI = 2,
    OpenAICompatible = 3,
    Custom = 4
}

/// <summary>
/// Configuração de provider externo — a chave NUNCA é armazenada;
/// apenas o nome da variável de ambiente que a contém (lida em runtime).
/// </summary>
public sealed class ExternalProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public ExternalProviderType Type { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKeyEnvVar { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
