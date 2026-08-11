using System.Net.Http.Json;
using System.Text.Json;
using FinAI.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FinAI.IntegrationTests.Infrastructure;

/// <summary>
/// Sube un PostgreSQL real (Testcontainers), aplica migrations + seed y crea el WebApplicationFactory.
/// Compartido entre todos los tests de integración (ICollectionFixture).
/// </summary>
public sealed class FinAiTestFixture : IAsyncLifetime
{
    public static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("finai_test")
        .WithUsername("finai")
        .WithPassword("finai")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
                builder.UseSetting("DevUser:Id", TestUserId.ToString());
                builder.UseSetting("Logging:LogLevel:Default", "None");
            });

        // Aplica migrations + seed no banco real
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinAiDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Cria um cliente HTTP com JSON padrão.
    /// </summary>
    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>
    /// Acesso direto ao DbContext para preparar dados (ex.: recurso de outro usuário).
    /// </summary>
    public async Task<FinAiDbContext> CreateDbContextAsync()
    {
        var scope = Factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<FinAiDbContext>();
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<FinAiTestFixture>;
