using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinAI.IntegrationTests.Infrastructure;

/// <summary>
/// Opções de JSON consistentes com a API (enums como strings).
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T?> ReadAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken = default)
        => await response.Content.ReadFromJsonAsync<T>(Options, cancellationToken);
}
