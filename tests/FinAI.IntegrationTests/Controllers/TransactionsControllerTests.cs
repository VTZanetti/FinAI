using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Api.DTOs.Accounts;
using FinAI.Api.DTOs.Transactions;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class TransactionsControllerTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public TransactionsControllerTests(FinAiTestFixture fixture)
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

    private async Task<AccountResponse> CreateAccountAsync(HttpClient client, string name = "Conta")
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name,
            type = "Checking",
            currency = "BRL",
            initialBalance = 1000m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadAsync<AccountResponse>())!;
    }

    [Fact]
    public async Task CreateTransaction_Expense_UpdatesAccountBalance()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "UBER *TRIP",
            amount = -27.90m,
            date = "2026-08-10",
            isRecurring = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tx = await response.ReadAsync<TransactionResponse>();
        tx.Should().NotBeNull();
        tx!.Description.Should().Be("UBER *TRIP");
        tx.Amount.Should().Be(-27.90m);
        tx.Type.ToString().Should().Be("Expense"); // derivado do sinal

        // Saldo da conta recalculado: 1000 - 27.90 = 972.10
        var accountResponse = await client.GetAsync($"/api/v1/accounts/{account.Id}");
        var updated = await accountResponse.ReadAsync<AccountResponse>();
        updated!.CurrentBalance.Should().Be(972.10m);
    }

    [Fact]
    public async Task CreateTransaction_Income_DerivesIncomeType()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Salário",
            amount = 5000.00m,
            date = "2026-08-05",
            isRecurring = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tx = await response.ReadAsync<TransactionResponse>();
        tx!.Type.ToString().Should().Be("Income");
        tx.IsRecurring.Should().BeTrue();

        var updated = await (await client.GetAsync($"/api/v1/accounts/{account.Id}")).ReadAsync<AccountResponse>();
        updated!.CurrentBalance.Should().Be(6000m); // 1000 + 5000
    }

    [Fact]
    public async Task CreateTransaction_AccountOfAnotherUser_ReturnsNotFound()
    {
        // Arrange: id de conta inexistente (não pertence ao usuário)
        var client = await ClientAsync();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = Guid.NewGuid(),
            description = "Alheia",
            amount = -10m,
            date = "2026-08-10"
        });

        // Assert: 404 (nunca 403)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTransaction_DuplicateExternalId_ReturnsConflict()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client);
        var payload = new
        {
            accountId = account.Id,
            description = "Importada",
            amount = -50m,
            date = "2026-08-01",
            externalId = "ext-20260801-001"
        };

        var first = await client.PostAsJsonAsync("/api/v1/transactions", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: mesmo ExternalId
        var second = await client.PostAsJsonAsync("/api/v1/transactions", payload);

        // Assert: deduplicação
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListTransactions_FiltersByPeriodAndSearch()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client);
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Mercado em julho",
            amount = -100m,
            date = "2026-07-15"
        });
        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Restaurante em agosto",
            amount = -80m,
            date = "2026-08-15"
        });

        // Act: período de agosto + busca
        var response = await client.GetAsync("/api/v1/transactions?from=2026-08-01&to=2026-08-31&search=Restaurante");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.ReadAsync<JsonElement>();
        envelope.GetProperty("totalItems").GetInt32().Should().Be(1);
        var items = envelope.GetProperty("items");
        items[0].GetProperty("description").GetString().Should().Be("Restaurante em agosto");
    }

    [Fact]
    public async Task ListTransactions_Pagination_ReturnsEnvelope()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client);
        for (var i = 1; i <= 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/transactions", new
            {
                accountId = account.Id,
                description = $"Transação {i}",
                amount = -10m * i,
                date = $"2026-08-0{i}"
            });
        }

        // Act: filtra pela conta criada (isola do restante dos dados do banco compartilhado)
        var response = await client.GetAsync($"/api/v1/transactions?accountId={account.Id}&page=1&pageSize=2");

        // Assert
        var envelope = await response.ReadAsync<JsonElement>();
        envelope.GetProperty("items").GetArrayLength().Should().Be(2);
        envelope.GetProperty("totalItems").GetInt32().Should().Be(5);
        envelope.GetProperty("totalPages").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetTransaction_NotFound_Returns404()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync($"/api/v1/transactions/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTransaction_RecalculatesBalance()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client); // 1000
        var create = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Para excluir",
            amount = -200m,
            date = "2026-08-10"
        });
        var tx = await create.ReadAsync<TransactionResponse>();

        // Act
        var response = await client.DeleteAsync($"/api/v1/transactions/{tx!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = await (await client.GetAsync($"/api/v1/accounts/{account.Id}")).ReadAsync<AccountResponse>();
        updated!.CurrentBalance.Should().Be(1000m); // volta ao inicial
    }

    [Fact]
    public async Task UpdateTransaction_ChangesTypeAndBalance()
    {
        // Arrange
        var client = await ClientAsync();
        var account = await CreateAccountAsync(client); // 1000
        var create = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.Id,
            description = "Original",
            amount = -300m,
            date = "2026-08-10"
        });
        var tx = await create.ReadAsync<TransactionResponse>();

        // Act: vira receita
        var response = await client.PutAsJsonAsync($"/api/v1/transactions/{tx!.Id}", new
        {
            accountId = account.Id,
            description = "Atualizada",
            amount = 300m,
            date = "2026-08-10",
            isRecurring = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTx = await response.ReadAsync<TransactionResponse>();
        updatedTx!.Type.ToString().Should().Be("Income");

        var updated = await (await client.GetAsync($"/api/v1/accounts/{account.Id}")).ReadAsync<AccountResponse>();
        updated!.CurrentBalance.Should().Be(1300m); // 1000 + 300
    }
}
