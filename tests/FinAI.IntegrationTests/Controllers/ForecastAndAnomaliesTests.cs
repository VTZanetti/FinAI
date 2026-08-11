using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Api.DTOs.Accounts;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class ForecastAndAnomaliesTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public ForecastAndAnomaliesTests(FinAiTestFixture fixture)
    {
        _fixture = fixture;
        _authenticatedClient = new Lazy<Task<HttpClient>>(async () =>
        {
            var (client, auth) = await _fixture.CreateAuthenticatedClientAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return client;
        });
    }

    private Task<HttpClient> ClientAsync() => _authenticatedClient.Value;

    private async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Forecast",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var account = await response.ReadAsync<AccountResponse>();
        return account!.Id;
    }

    /// <summary>Busca o id da categoria do sistema "Other" (para baseline estável).</summary>
    private async Task<Guid> GetOtherCategoryIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/categories");
        var list = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        var other = list!.First(c => c.GetProperty("name").GetString() == "Other" && c.GetProperty("isSystem").GetBoolean());
        return other.GetProperty("id").GetGuid();
    }

    private async Task SeedTransactionsAsync(HttpClient client, Guid accountId, Guid otherCategoryId)
    {
        // Histórico: 20 meses com despesas normais (~150-200) na categoria "Other"
        var random = new Random(42);
        for (var i = 1; i <= 20; i++)
        {
            var month = DateOnly.FromDateTime(DateTime.Today).AddMonths(-i);
            await client.PostAsJsonAsync("/api/v1/transactions", new
            {
                accountId,
                description = $"Gasto mês {i}",
                amount = -(150m + random.Next(0, 50)),
                date = $"{month.Year:0000}-{month.Month:00}-15",
                categoryId = otherCategoryId
            });
        }

        // Receitas
        var current = DateOnly.FromDateTime(DateTime.Today);
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId,
            description = "Salário",
            amount = 5000m,
            date = $"{current.Year:0000}-{current.Month:00}-05"
        });
    }

    [Fact]
    public async Task CashFlowForecast_ReturnsMethodAndPoints()
    {
        // Arrange
        var client = await ClientAsync();
        var accountId = await CreateAccountAsync(client);
        var otherCategoryId = await GetOtherCategoryIdAsync(client);
        await SeedTransactionsAsync(client, accountId, otherCategoryId);

        // Act
        var response = await client.GetAsync("/api/v1/forecast/cash-flow?months=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();
        json.GetProperty("method").GetString().Should().Be("weighted_moving_average");
        json.GetProperty("forecast").GetArrayLength().Should().Be(3);
        json.GetProperty("confidence").GetProperty("level").GetString().Should().BeOneOf("high", "medium", "low");
    }

    [Fact]
    public async Task CashFlowForecast_InvalidMonths_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/forecast/cash-flow?months=25");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anomalies_ZScore_DetectsExtremeTransaction()
    {
        // Arrange: histórico normal na categoria "Other" + transação extrema na mesma categoria
        var client = await ClientAsync();
        var accountId = await CreateAccountAsync(client);
        var otherCategoryId = await GetOtherCategoryIdAsync(client);
        await SeedTransactionsAsync(client, accountId, otherCategoryId);

        var today = DateOnly.FromDateTime(DateTime.Today);
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId,
            description = "SUPERMERCADO EXTRA ANÔMALO",
            amount = -5000m,
            date = $"{today.Year:0000}-{today.Month:00}-20",
            categoryId = otherCategoryId
        });

        // Act
        var from = $"{today.Year:0000}-{today.Month:00}-01";
        var to = $"{today.Year:0000}-{today.Month:00}-31";
        var response = await client.GetAsync($"/api/v1/anomalies?from={from}&to={to}&method=zscore");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();
        json.GetProperty("method").GetString().Should().Be("zscore");
        var items = json.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        var anomalies = items.EnumerateArray().Where(i => i.GetProperty("anomaly").GetBoolean()).ToList();
        anomalies.Should().NotBeEmpty();
        anomalies.Should().Contain(i => i.GetProperty("description").GetString()!.Contains("ANÔMALO"));
    }

    [Fact]
    public async Task AnomaliesCheck_ExtremeValue_ReturnsAnomalyTrue()
    {
        // Arrange
        var client = await ClientAsync();
        var accountId = await CreateAccountAsync(client);
        var otherCategoryId = await GetOtherCategoryIdAsync(client);
        await SeedTransactionsAsync(client, accountId, otherCategoryId);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/anomalies/check", new
        {
            description = "GASTO ESTRANHO",
            amount = -9000m,
            categoryId = otherCategoryId
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();
        json.GetProperty("anomaly").GetBoolean().Should().BeTrue();
        json.GetProperty("suggestedAction").GetString().Should().Be("review");
        json.GetProperty("score").GetDecimal().Should().BeInRange(0m, 1m);
    }

    [Fact]
    public async Task AnomaliesCheck_NormalValue_ReturnsAnomalyFalse()
    {
        // Arrange
        var client = await ClientAsync();
        var accountId = await CreateAccountAsync(client);
        var otherCategoryId = await GetOtherCategoryIdAsync(client);
        await SeedTransactionsAsync(client, accountId, otherCategoryId);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/anomalies/check", new
        {
            description = "GASTO NORMAL",
            amount = -180m,
            categoryId = otherCategoryId
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();
        json.GetProperty("anomaly").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Anomalies_WithoutToken_Returns401()
    {
        // Arrange
        var anonymous = _fixture.CreateClient();

        // Act
        var response = await anonymous.GetAsync("/api/v1/anomalies?from=2026-01-01&to=2026-12-31");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
