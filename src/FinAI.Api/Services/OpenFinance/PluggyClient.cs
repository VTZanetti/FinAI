using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinAI.Api.Services.OpenFinance.Models;
using FinAI.Api.Services.OpenFinance.Options;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.OpenFinance;

public interface IPluggyClient
{
    Task<PluggyAuthResponse> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PluggyAccountDto>> GetAccountsAsync(string apiKey, string itemId, CancellationToken cancellationToken = default);
    Task<PluggyTransactionPage> GetTransactionsAsync(string apiKey, string itemId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PluggyConnectTokenResponse> CreateConnectTokenAsync(string clientUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Cliente HTTP da API Pluggy (/auth, /accounts, /transactions, /connect-token).
/// </summary>
public class PluggyClient : IPluggyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PluggyOptions _options;

    public PluggyClient(IHttpClientFactory httpClientFactory, IOptions<PluggyOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<PluggyAuthResponse> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("pluggy");
        client.Timeout = TimeSpan.FromSeconds(30);

        var response = await client.PostAsJsonAsync("/auth", new
        {
            clientId = _options.ClientId,
            clientSecret = _options.ClientSecret
        }, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PluggyAuthResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Pluggy auth returned empty response");
    }

    public async Task<IReadOnlyList<PluggyAccountDto>> GetAccountsAsync(string apiKey, string itemId, CancellationToken cancellationToken = default)
    {
        var client = CreateDataClient(apiKey);
        var response = await client.GetAsync($"/accounts?itemId={itemId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AccountsResponse>(JsonOptions, cancellationToken);
        return result?.Results ?? [];
    }

    public async Task<PluggyTransactionPage> GetTransactionsAsync(string apiKey, string itemId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var client = CreateDataClient(apiKey);
        var response = await client.GetAsync($"/transactions?itemId={itemId}&page={page}&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TransactionsResponse>(JsonOptions, cancellationToken);
        return new PluggyTransactionPage(
            result?.Results ?? [],
            result?.Page ?? page,
            result?.PageSize ?? pageSize,
            result?.TotalPages ?? 0,
            result?.Total ?? 0);
    }

    public async Task<PluggyConnectTokenResponse> CreateConnectTokenAsync(string clientUserId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("pluggy");
        client.Timeout = TimeSpan.FromSeconds(30);

        var response = await client.PostAsJsonAsync("/connect-token", new
        {
            clientUserId
        }, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PluggyConnectTokenResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Pluggy connect-token returned empty response");
    }

    private HttpClient CreateDataClient(string apiKey)
    {
        var client = _httpClientFactory.CreateClient("pluggy");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Remove("X-API-KEY");
        client.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
        return client;
    }

    private sealed class AccountsResponse
    {
        public List<PluggyAccountDto>? Results { get; set; }
    }

    private sealed class TransactionsResponse
    {
        public List<PluggyTransactionDto>? Results { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int Total { get; set; }
    }
}