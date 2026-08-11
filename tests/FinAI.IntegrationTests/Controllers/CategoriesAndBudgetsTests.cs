using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Api.DTOs.Budgets;
using FinAI.Api.DTOs.Categories;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class CategoriesAndBudgetsTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public CategoriesAndBudgetsTests(FinAiTestFixture fixture)
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

    // ── Categories ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListCategories_IncludesSystemCategories()
    {
        // Act
        var response = await (await ClientAsync()).GetAsync("/api/v1/categories");

        // Assert: seed aplicado na migration => categorias do sistema presentes
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.ReadAsync<List<CategoryResponse>>();
        categories.Should().NotBeNull();
        categories!.Any(c => c.IsSystem && c.Name == "Food").Should().BeTrue();
        categories.Any(c => c.IsSystem && c.Name == "Other" && c.Subcategory is null).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSystemCategory_ReturnsForbidden()
    {
        // Arrange: busca categoria do sistema
        var categories = await (await (await ClientAsync()).GetAsync("/api/v1/categories")).ReadAsync<List<CategoryResponse>>();
        var system = categories!.First(c => c.IsSystem);

        // Act
        var response = await (await ClientAsync()).PutAsJsonAsync($"/api/v1/categories/{system.Id}", new { name = "Hackeada" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteSystemCategory_ReturnsForbidden()
    {
        // Arrange
        var categories = await (await (await ClientAsync()).GetAsync("/api/v1/categories")).ReadAsync<List<CategoryResponse>>();
        var system = categories!.First(c => c.IsSystem);

        // Act
        var response = await (await ClientAsync()).DeleteAsync($"/api/v1/categories/{system.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCategory_ThenDelete_ReturnsNoContent()
    {
        // Arrange
        var create = await (await ClientAsync()).PostAsJsonAsync("/api/v1/categories", new { name = "Pets", subcategory = "Vet" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await create.ReadAsync<CategoryResponse>();

        // Act
        var delete = await (await ClientAsync()).DeleteAsync($"/api/v1/categories/{category!.Id}");

        // Assert
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateCategory_Duplicate_ReturnsConflict()
    {
        // Arrange
        await (await ClientAsync()).PostAsJsonAsync("/api/v1/categories", new { name = "Duplicada" });

        // Act
        var response = await (await ClientAsync()).PostAsJsonAsync("/api/v1/categories", new { name = "Duplicada" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Budgets ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBudget_ShowsProgress()
    {
        // Arrange: conta + transações de despesa na categoria "Food/Restaurant" (sistema)
        var accountResponse = await (await ClientAsync()).PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Budget",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        var account = await accountResponse.ReadAsync<JsonElement>();

        var categories = await (await (await ClientAsync()).GetAsync("/api/v1/categories")).ReadAsync<List<CategoryResponse>>();
        var foodRestaurant = categories!.First(c => c.Name == "Food" && c.Subcategory == "Restaurant");

        await (await ClientAsync()).PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = account.GetProperty("id").GetGuid(),
            description = "Restaurante",
            amount = -342.10m,
            date = "2026-08-10",
            categoryId = foodRestaurant.Id
        });

        // Act
        var response = await (await ClientAsync()).PostAsJsonAsync("/api/v1/budgets", new
        {
            categoryId = foodRestaurant.Id,
            month = 8,
            year = 2026,
            limitAmount = 800.00m
        });

        // Assert: spentAmount = 342.10, progress = 42.7625 ≈ 42.76
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var budget = await response.ReadAsync<BudgetResponse>();
        budget!.LimitAmount.Should().Be(800.00m);
        budget.SpentAmount.Should().Be(342.10m);
        budget.ProgressPercent.Should().Be(42.76m);
    }

    [Fact]
    public async Task CreateBudget_DuplicatePeriod_ReturnsConflict()
    {
        // Arrange
        var categories = await (await (await ClientAsync()).GetAsync("/api/v1/categories")).ReadAsync<List<CategoryResponse>>();
        var other = categories!.First(c => c.Name == "Other");

        var payload = new { categoryId = other.Id, month = 8, year = 2026, limitAmount = 100m };
        var first = await (await ClientAsync()).PostAsJsonAsync("/api/v1/budgets", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var second = await (await ClientAsync()).PostAsJsonAsync("/api/v1/budgets", payload);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateBudget_InvalidMonth_ReturnsBadRequest()
    {
        // Act
        var response = await (await ClientAsync()).PostAsJsonAsync("/api/v1/budgets", new
        {
            categoryId = Guid.NewGuid(),
            month = 13,
            year = 2026,
            limitAmount = 100m
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBudget_NotFound_Returns404()
    {
        // Act
        var response = await (await ClientAsync()).GetAsync($"/api/v1/budgets/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateBudget_ChangesLimit()
    {
        // Arrange
        var categories = await (await (await ClientAsync()).GetAsync("/api/v1/categories")).ReadAsync<List<CategoryResponse>>();
        var other = categories!.First(c => c.Name == "Other");
        var create = await (await ClientAsync()).PostAsJsonAsync("/api/v1/budgets", new
        {
            categoryId = other.Id,
            month = 9,
            year = 2026,
            limitAmount = 500m
        });
        var budget = await create.ReadAsync<BudgetResponse>();

        // Act
        var response = await (await ClientAsync()).PutAsJsonAsync($"/api/v1/budgets/{budget!.Id}", new { limitAmount = 750m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadAsync<BudgetResponse>();
        updated!.LimitAmount.Should().Be(750m);
    }
}
