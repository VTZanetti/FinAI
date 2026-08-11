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
public class AnalyticsControllerTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public AnalyticsControllerTests(FinAiTestFixture fixture)
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

    private async Task<Guid> CreateAccountWithTransactionsAsync()
    {
        var client = await ClientAsync();

        var accountResponse = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Analytics",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        var account = await accountResponse.ReadAsync<AccountResponse>();

        // Receitas + despesas em agosto/2026
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account!.Id,
            description = "Salário",
            amount = 5000m,
            date = "2026-08-05"
        });
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Restaurante",
            amount = -342.10m,
            date = "2026-08-10"
        });
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Streaming",
            amount = -55.90m,
            date = "2026-08-01",
            isRecurring = true
        });

        return account.Id;
    }

    [Fact]
    public async Task SpendingSummary_ReturnsTotalsAndComposition()
    {
        // Arrange
        await CreateAccountWithTransactionsAsync();
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/analytics/spending-summary?from=2026-08-01&to=2026-08-31");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();

        var totals = json.GetProperty("totals");
        totals.GetProperty("income").GetDecimal().Should().Be(5000m);
        totals.GetProperty("expenses").GetDecimal().Should().BeApproximately(398.0m, 0.5m); // 342.10 + 55.90
        totals.GetProperty("balance").GetDecimal().Should().BeApproximately(4602.0m, 0.5m);

        var recurring = json.GetProperty("recurring");
        recurring.GetProperty("amount").GetDecimal().Should().BeApproximately(55.90m, 0.1m);

        var byCategory = json.GetProperty("byCategory");
        byCategory.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SpendingSummary_FromAfterTo_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/analytics/spending-summary?from=2026-08-31&to=2026-08-01");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Behavior_ReturnsHumanizedInsights()
    {
        // Arrange
        var client = await ClientAsync();

        // Transações no mês atual e no anterior (variação > 10%)
        var accountResponse = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Behavior",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        var account = await accountResponse.ReadAsync<AccountResponse>();

        // Meses de referência: ventana atual = últimos 3 meses; ventana anterior = 3 meses antes
        var fourMonthsAgo = DateOnly.FromDateTime(DateTime.Today).AddMonths(-4);
        var currentMonth = DateOnly.FromDateTime(DateTime.Today);

        // Ventana anterior (4 meses atrás): gastos baixos
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account!.Id,
            description = "Comida",
            amount = -200m,
            date = $"{fourMonthsAgo.Year:0000}-{fourMonthsAgo.Month:00}-10"
        });
        // Ventana atual: gastos altos
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Comida",
            amount = -1000m,
            date = $"{currentMonth.Year:0000}-{currentMonth.Month:00}-10"
        });

        // Act
        var response = await client.GetAsync("/api/v1/analytics/behavior?months=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();
        var insights = json.GetProperty("insights");

        insights.GetArrayLength().Should().BeGreaterThan(0);
        // Variação de 1000 vs 200 = +400% → category_increase
        var hasMessage = insights.EnumerateArray().Any(i => i.GetProperty("message").GetString()!.Contains("aumentaram"));
        hasMessage.Should().BeTrue();
    }

    [Fact]
    public async Task Behavior_InvalidMonths_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/analytics/behavior?months=13");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MonthlyTrend_ReturnsContinuousSeries()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/analytics/monthly-trend?months=12");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadAsync<JsonElement>();
        var trend = json.GetProperty("trend");

        trend.GetArrayLength().Should().Be(12);
        var first = trend[0].GetProperty("month").GetString();
        first.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Analytics_WithoutToken_Returns401()
    {
        // Arrange
        var anonymous = _fixture.CreateClient();

        // Act
        var response = await anonymous.GetAsync("/api/v1/analytics/spending-summary?from=2026-01-01&to=2026-12-31");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
