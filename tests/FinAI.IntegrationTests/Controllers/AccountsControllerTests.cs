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
public class AccountsControllerTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly HttpClient _client;

    public AccountsControllerTests(FinAiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var (client, _) = await _fixture.CreateAuthenticatedClientAsync();
        return client;
    }

    [Fact]
    public async Task CreateAccount_ReturnsCreatedWithCurrentBalance()
    {
        // Arrange
        var client = await AuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Corrente Nubank",
            type = "Checking",
            currency = "BRL",
            initialBalance = 1250.75m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var account = await response.ReadAsync<AccountResponse>();
        account.Should().NotBeNull();
        account!.Name.Should().Be("Conta Corrente Nubank");
        account.InitialBalance.Should().Be(1250.75m);
        account.CurrentBalance.Should().Be(1250.75m);
        account.Type.ToString().Should().Be("Checking");
    }

    [Fact]
    public async Task CreateAccount_InvalidType_ReturnsBadRequest()
    {
        // Arrange
        var client = await AuthenticatedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Invalida",
            type = "Pix",
            currency = "BRL",
            initialBalance = 0m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAccount_ByOtherUser_ReturnsNotFound()
    {
        // Arrange: usuário A cria conta
        var (userA, _) = await _fixture.CreateAuthenticatedClientAsync();
        var createResponse = await userA.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Alheia",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.ReadAsync<AccountResponse>();

        // Act: usuário B tenta acessar
        var (userB, _) = await _fixture.CreateAuthenticatedClientAsync();
        var response = await userB.GetAsync($"/api/v1/accounts/{created!.Id}");

        // Assert: 404 — nunca 403 (não vaza existência)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAccount_ChangesNameTypeCurrency_KeepsInitialBalance()
    {
        // Arrange
        var client = await AuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Antiga",
            type = "Checking",
            currency = "BRL",
            initialBalance = 500m
        });
        var created = await createResponse.ReadAsync<AccountResponse>();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/accounts/{created!.Id}", new
        {
            name = "Conta Nova",
            type = "Savings",
            currency = "USD"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<AccountResponse>();
        updated!.Name.Should().Be("Conta Nova");
        updated.Type.ToString().Should().Be("Savings");
        updated.Currency.Should().Be("USD");
        updated.InitialBalance.Should().Be(500m); // não editável
    }

    [Fact]
    public async Task DeleteAccount_WithoutTransactions_ReturnsNoContent()
    {
        // Arrange
        var client = await AuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Descartável",
            type = "Cash",
            currency = "BRL",
            initialBalance = 0m
        });
        var created = await createResponse.ReadAsync<AccountResponse>();

        // Act
        var response = await client.DeleteAsync($"/api/v1/accounts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAccount_NotFound_Returns404()
    {
        // Arrange
        var client = await AuthenticatedClientAsync();

        // Act
        var response = await client.DeleteAsync($"/api/v1/accounts/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListAccounts_ReturnsPagedEnvelope()
    {
        // Arrange
        var client = await AuthenticatedClientAsync();
        await client.PostAsJsonAsync("/api/v1/accounts", new { name = "Conta 1", type = "Checking", currency = "BRL", initialBalance = 0m });
        await client.PostAsJsonAsync("/api/v1/accounts", new { name = "Conta 2", type = "Savings", currency = "BRL", initialBalance = 0m });

        // Act
        var response = await client.GetAsync("/api/v1/accounts?page=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.ReadAsync<JsonElement>();
        envelope.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        envelope.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }
}
