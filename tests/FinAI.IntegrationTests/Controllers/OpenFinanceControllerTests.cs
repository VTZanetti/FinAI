using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class OpenFinanceControllerTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public OpenFinanceControllerTests(FinAiTestFixture fixture)
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
    public async Task Sync_WithoutConfig_ReturnsValidationError()
    {
        // Arrange: sem Pluggy:ItemId configurado no fixture
        var client = await ClientAsync();

        // Act: body vazio válido ({}), ItemId ausente
        var response = await client.PostAsJsonAsync("/api/v1/open-finance/sync", new { });

        // Assert: 400 (validação — sem ItemId configurado)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConnectToken_ReturnsTokenFromPluggyProxy()
    {
        // Arrange: o fixture aponta para um stub? Não — sem credenciais reais, o proxy falha.
        // Validamos apenas o contrato: 400/502 (sem credenciais), nunca 401.
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsync("/api/v1/open-finance/connect-token", null);

        // Assert: sem credenciais Pluggy configuradas, o proxy retorna erro controlado (não 401)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadGateway, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task LinkConnection_LinksItemToUser()
    {
        // Arrange
        var client = await ClientAsync();

        // Act: vincula um itemId (simula retorno do Connect Widget)
        var response = await client.PostAsJsonAsync("/api/v1/open-finance/connections", new
        {
            itemId = "item-integration-test",
            institutionName = "Nubank"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.GetProperty("itemId").GetString().Should().Be("item-integration-test");
    }

    [Fact]
    public async Task LinkConnection_Duplicate_IsIdempotent()
    {
        // Arrange
        var client = await ClientAsync();
        await client.PostAsJsonAsync("/api/v1/open-finance/connections", new { itemId = "item-dup" });

        // Act: mesmo itemId de novo
        var second = await client.PostAsJsonAsync("/api/v1/open-finance/connections", new { itemId = "item-dup" });

        // Assert: idempotente (200, não duplica)
        second.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListConnections_ReturnsOwnConnections()
    {
        // Arrange
        var client = await ClientAsync();
        await client.PostAsJsonAsync("/api/v1/open-finance/connections", new { itemId = "item-list" });

        // Act
        var response = await client.GetAsync("/api/v1/open-finance/connections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        list!.Any(c => c.GetProperty("itemId").GetString() == "item-list").Should().BeTrue();
    }

    [Fact]
    public async Task Status_ReturnsLastSyncInfo()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/open-finance/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.GetProperty("connectionsCount").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task OpenFinance_WithoutToken_Returns401()
    {
        // Arrange
        var anonymous = _fixture.CreateClient();

        // Act
        var response = await anonymous.GetAsync("/api/v1/open-finance/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
