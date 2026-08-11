using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinAI.Api.DTOs.Auth;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class AuthFlowTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly HttpClient _client;

    public AuthFlowTests(FinAiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private static void SetBearer(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Register_Login_Access_Refresh_Logout_FullFlow()
    {
        // 1. Register
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "flow@test.com",
            password = FinAiTestFixture.TestPassword,
            firstName = "Fluxo",
            lastName = "Teste"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.ExpiresIn.Should().Be(900); // 15 min

        // 2. Acesso com token
        SetBearer(_client, auth.AccessToken);
        var accounts = await _client.GetAsync("/api/v1/accounts");
        accounts.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Refresh (rotação)
        var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        refreshed!.AccessToken.Should().NotBe(auth.AccessToken);
        refreshed.RefreshToken.Should().NotBe(auth.RefreshToken); // rotativo

        // 4. Token antigo (revogado na rotação) → reuso detectado → 401 + família revogada
        var reuse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // O novo token também é revogado (família) → 401
        var familyKilled = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refreshed.RefreshToken });
        familyKilled.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. Logout com token já revogado → idempotente 204
        var logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refreshed.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AccessWithoutToken_Returns401()
    {
        // Act: sem Authorization header
        var response = await _client.GetAsync("/api/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessWithInvalidToken_Returns401()
    {
        // Arrange
        SetBearer(_client, "token-invalido");

        // Act
        var response = await _client.GetAsync("/api/v1/accounts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange
        await _fixture.RegisterUserAsync("login@test.com", _client);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "login@test.com",
            password = "SenhaErrada!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        await _fixture.RegisterUserAsync("login2@test.com", _client);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "login2@test.com",
            password = FinAiTestFixture.TestPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        // Arrange
        await _fixture.RegisterUserAsync("dup@test.com", _client);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "dup@test.com",
            password = FinAiTestFixture.TestPassword,
            firstName = "Dup",
            lastName = "User"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "weak@test.com",
            password = "12345",
            firstName = "Weak",
            lastName = "User"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserRole_CannotAccessAdminEndpoints_Returns403()
    {
        // Arrange
        var (client, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_CanAccessAdminEndpoints()
    {
        // Arrange
        var adminClient = await _fixture.CreateAdminClientAsync();

        // Act
        var response = await adminClient.GetAsync("/api/v1/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_CanReadAuditLogs()
    {
        // Arrange: criar conta gera audit log
        var (userClient, _) = await _fixture.CreateAuthenticatedClientAsync();
        await userClient.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Auditada",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });

        var adminClient = await _fixture.CreateAdminClientAsync();

        // Act
        var response = await adminClient.GetAsync("/api/v1/admin/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<List<FinAI.Api.DTOs.Admin.AuditLogResponse>>(TestJson.Options);
        logs.Should().NotBeNull();
        logs!.Any(l => l.Action == "account.create").Should().BeTrue();
    }

    [Fact]
    public async Task CrossUserAccess_Returns404()
    {
        // Arrange: usuário A cria conta
        var (userA, _) = await _fixture.CreateAuthenticatedClientAsync();
        var create = await userA.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta do A",
            type = "Checking",
            currency = "BRL",
            initialBalance = 0m
        });
        var account = await create.Content.ReadFromJsonAsync<FinAI.Api.DTOs.Accounts.AccountResponse>(TestJson.Options);

        // Usuário B tenta acessar a conta do A
        var (userB, _) = await _fixture.CreateAuthenticatedClientAsync();

        // Act
        var response = await userB.GetAsync($"/api/v1/accounts/{account!.Id}");

        // Assert: 404 (nunca 403 — não vaza existência)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logout_ThenRefreshWithRevokedToken_Returns401()
    {
        // Arrange
        var (client, auth) = await _fixture.CreateAuthenticatedClientAsync();

        // Act: logout
        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = auth.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Refresh com token revogado
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });

        // Assert
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
