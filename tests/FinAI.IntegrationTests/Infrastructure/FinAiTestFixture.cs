using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinAI.Api.Data;
using FinAI.Api.DTOs.Auth;
using FinAI.Api.Models;
using FinAI.Api.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FinAI.IntegrationTests.Infrastructure;

/// <summary>
/// Sube un PostgreSQL real (Testcontainers), aplica migrations + seed, crea roles
/// y el WebApplicationFactory. Compartido entre todos los tests de integración (ICollectionFixture).
/// </summary>
public sealed class FinAiTestFixture : IAsyncLifetime
{
    public const string TestPassword = "S3nh@Forte!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("finai_test")
        .WithUsername("finai")
        .WithPassword("finai")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Aplica migrations ANTES de criar a factory — o host inicia o seed de papéis
        // no startup (Program.cs), que exige as tabelas de Identity já existentes.
        var services = new ServiceCollection();
        services.AddDbContext<FinAiDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString(), n => n.UseVector()));
        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<FinAiDbContext>();
        await db.Database.MigrateAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
                builder.UseSetting("Jwt:SigningKey", TestSigningKey);
                builder.UseSetting("RateLimiting:Enabled", "false");
                builder.UseSetting("Ai:Enabled", "false"); // testes rodam em modo rules-only (sem Ollama)
                builder.UseSetting("Documents:ProcessingEnabled", "false"); // sem pipeline async nos testes
                builder.UseSetting("Pluggy:ItemId", ""); // sem ItemId configurado nos testes
                builder.UseSetting("Logging:LogLevel:Default", "None");
            });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Cria um cliente HTTP sem autenticação.</summary>
    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>
    /// Registra um usuário e retorna o resultado de autenticação (tokens).
    /// </summary>
    public async Task<AuthResponse> RegisterUserAsync(string email, HttpClient? client = null)
    {
        client ??= CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Teste",
            lastName = "User"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }

    /// <summary>Cria um cliente autenticado com um usuário novo.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClientAsync(string? email = null)
    {
        var client = CreateClient();
        var uniqueEmail = email ?? $"user-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterUserAsync(uniqueEmail, client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    /// <summary>
    /// Cria um usuário admin e retorna cliente autenticado com papel Admin.
    /// </summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@test.com";
        var client = CreateClient();
        var auth = await RegisterUserAsync(email, client);

        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FinAiUser>>();
        var user = await userManager.FindByEmailAsync(email);
        await userManager.AddToRoleAsync(user!, AuthService.RoleAdmin);

        // O token emitido no register não tem o papel Admin — re-autentica para obter claims atualizados
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = TestPassword
        });
        login.EnsureSuccessStatusCode();
        var adminAuth = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);

        return client;
    }

    /// <summary>Acesso direto ao DbContext para preparar dados.</summary>
    public async Task<FinAiDbContext> CreateDbContextAsync()
    {
        var scope = Factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<FinAiDbContext>();
    }

    public static string TestSigningKey =
        "integration-test-signing-key-64-characters-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
}
