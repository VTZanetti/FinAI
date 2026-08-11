using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinAI.Api.DTOs.Accounts;
using FinAI.Api.DTOs.Transactions;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class AiControllerTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public AiControllerTests(FinAiTestFixture fixture)
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

    [Fact]
    public async Task Classify_UberDescription_ReturnsRulesSource()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/ai/classify", new
        {
            description = "UBER *TRIP",
            amount = -27.90m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.GetProperty("source").GetString().Should().Be("rules");
        json.GetProperty("category").GetString().Should().Be("Transportation");
        json.GetProperty("subcategory").GetString().Should().Be("Ride Sharing");
        json.GetProperty("confidence").GetDecimal().Should().BeGreaterThan(0.8m);
    }

    [Fact]
    public async Task Classify_UnknownDescription_ReturnsFallbackOrValidSource()
    {
        // Arrange: sem Ollama no CI, LLM indisponível → fallback
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/ai/classify", new
        {
            description = "TRANSFERENCIA PIX JOAO DA SILVA",
            amount = -50m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var source = json.GetProperty("source").GetString();
        source.Should().BeOneOf("rules", "cached", "llm", "fallback");
    }

    [Fact]
    public async Task Classify_EmptyDescription_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/ai/classify", new
        {
            description = "",
            amount = -10m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTransaction_WithoutCategory_AssignsClassification()
    {
        // Arrange
        var client = await ClientAsync();
        var accountResponse = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta IA",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        var account = await accountResponse.ReadAsync<AccountResponse>();

        // Act: transação sem categoryId → classificação automática por regras
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account!.Id,
            description = "IFOOD *PEDIDO 42",
            amount = -89.90m,
            date = "2026-08-10"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tx = await response.ReadAsync<TransactionResponse>();
        tx!.Classification.Should().NotBeNull();
        tx.Classification!.Source.Should().Be("rules");
        tx.Classification.Category.Should().Be("Food");
        tx.Category.Should().NotBeNull();
        tx.Category!.Name.Should().Be("Food");
    }

    [Fact]
    public async Task FinancialAdvisor_WithoutLlm_ReturnsServiceUnavailable()
    {
        // Arrange: Ollama não roda nos testes → LLM indisponível
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/ai/financial-advisor", new
        {
            question = "Quanto gastei este mês?"
        });

        // Assert: sem LLM, 503 com mensagem amigável (dados calculados)
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task FinancialAdvisor_EmptyQuestion_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/ai/financial-advisor", new
        {
            question = ""
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ai_WithoutToken_Returns401()
    {
        // Arrange
        var anonymous = _fixture.CreateClient();

        // Act
        var response = await anonymous.PostAsJsonAsync("/api/v1/ai/classify", new
        {
            description = "UBER",
            amount = -10m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
