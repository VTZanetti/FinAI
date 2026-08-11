using System.Text;
using System.Text.Json.Serialization;
using FinAI.Api.Data;
using FinAI.Api.Middleware;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Security;
using FinAI.Api.Services;
using FinAI.Api.Services.Accounts;
using FinAI.Api.Services.AI;
using FinAI.Api.Services.AI.External;
using FinAI.Api.Services.Analytics;
using FinAI.Api.Services.AnomalyDetection;
using FinAI.Api.Services.AnomalyDetection.Models;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.Documents;
using FinAI.Api.Services.Forecasting;
using FinAI.Api.Services.OpenFinance;
using FinAI.Api.Services.OpenFinance.Background;
using FinAI.Api.Services.OpenFinance.Options;
using FinAI.Api.Services.Auth;
using FinAI.Api.Services.Budgets;
using FinAI.Api.Services.Categories;
using FinAI.Api.Services.Transactions;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Pgvector.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Background services não devem derrubar o host (ex.: cancellation no shutdown)
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// ── Serilog (console + arquivo com rotação) ────────────────────────────────
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/finai-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14));

// ── Controllers + validação ────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Enums como string no JSON (contrato: "type": "Checking")
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);

// ── EF Core + PostgreSQL ───────────────────────────────────────────────────
builder.Services.AddDbContext<FinAiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseVector()));

// ── Identity ───────────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<FinAiUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<FinAiDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ── JWT ────────────────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

// ── DI: Security ───────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// ── Rate limiting ──────────────────────────────────────────────────────────
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

// ── IA: LLM (Ollama) ───────────────────────────────────────────────────────
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.AddHttpClient("ollama", client =>
{
    var baseUrl = builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
});
builder.Services.AddScoped<ILlmProvider, OllamaLlmProvider>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
builder.Services.AddScoped<IRuleClassifier, RuleClassifier>();
builder.Services.AddScoped<IClassificationService, ClassificationService>();
builder.Services.AddScoped<IFinancialAdvisorService, FinancialAdvisorService>();

// ── Providers externos (v0.7) ──────────────────────────────────────────────
builder.Services.AddHttpClient("external-llm");
builder.Services.AddSingleton<IExternalProviderRegistry, ExternalProviderRegistry>();
builder.Services.AddSingleton<IExternalLlmProviderFactory, ExternalLlmProviderFactory>();

// ── Open Finance / Pluggy (v0.8) ───────────────────────────────────────────
builder.Services.Configure<PluggyOptions>(builder.Configuration.GetSection(PluggyOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("pluggy", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Pluggy:BaseUrl"] ?? "https://api.pluggy.ai");
});
builder.Services.AddScoped<IPluggyClient, PluggyClient>();
builder.Services.AddScoped<IPluggyAuthService, PluggyAuthService>();
builder.Services.AddScoped<IOpenFinanceRepository, OpenFinanceRepository>();
builder.Services.AddScoped<IOpenFinanceSyncService, OpenFinanceSyncService>();
builder.Services.AddScoped<IOpenFinanceConnectionService, OpenFinanceConnectionService>();
builder.Services.AddScoped<IOpenFinanceStatusService, OpenFinanceStatusService>();
builder.Services.AddHostedService<OpenFinanceSyncHostedService>();

// ── DI: Repositories ───────────────────────────────────────────────────────
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBudgetRepository, BudgetRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IClassificationCacheRepository, ClassificationCacheRepository>();

// ── DI: Services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISpendingAnalyzer, SpendingAnalyzer>();
builder.Services.AddScoped<IBehaviorAnalyzer, BehaviorAnalyzer>();
builder.Services.AddScoped<IMonthlyTrendAnalyzer, MonthlyTrendAnalyzer>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// ── DI: Forecasting & Anomalias (v0.5) ─────────────────────────────────────
builder.Services.Configure<AnomalyDetectionOptions>(builder.Configuration.GetSection(AnomalyDetectionOptions.SectionName));
builder.Services.AddScoped<IMovingAverageForecaster, MovingAverageForecaster>();
builder.Services.AddScoped<IForecastService, ForecastService>();
builder.Services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();

// ── DI: Documentos & RAG (v0.6) ────────────────────────────────────────────
builder.Services.Configure<DocumentOptions>(builder.Configuration.GetSection(DocumentOptions.SectionName));
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ITextExtractor, PdfTextExtractor>();
builder.Services.AddScoped<IChunker, TokenChunker>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IVectorStore, PgVectorStore>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

// O processador de documentos (BackgroundService) só roda quando habilitado
// (testes de integração desligam para evitar o pipeline async real).
if (builder.Configuration.GetValue<bool>("Documents:ProcessingEnabled", true))
{
    builder.Services.AddSingleton<DocumentProcessor>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DocumentProcessor>());
    builder.Services.AddScoped<IDocumentProcessor>(sp => sp.GetRequiredService<DocumentProcessor>());
}
else
{
    builder.Services.AddScoped<IDocumentProcessor, NoopDocumentProcessor>();
}

// ── Swagger/OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FinAI API",
        Version = "v1",
        Description = "Financial Intelligence as a Service — API financeira inteligente."
    });

    // Botão Authorize (Bearer)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole o token JWT: Bearer {token}"
    });

    // OpenApi 2.x: o security requirement usa referências de esquema
    c.AddSecurityRequirement(doc =>
    {
        var reference = new OpenApiSecuritySchemeReference(
            "Bearer",
            doc,
            "Bearer");
        return new OpenApiSecurityRequirement
        {
            { reference, new List<string>() }
        };
    });
});

// ── Health checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FinAiDbContext>("postgres");

var app = builder.Build();

// ── Seed de papéis (User, Admin) — garante que existam em qualquer ambiente ─
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in new[] { AuthService.RoleUser, AuthService.RoleAdmin })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }
}

// ── Migrations automáticas (Docker/Production) ─────────────────────────────
// Em dev/testes, as migrations são aplicadas via CLI/Testcontainers.
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FinAiDbContext>();
    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < 5)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Migration attempt {Attempt}/5 failed; retrying in 3s", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// ── Pipeline ───────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinAI API v1"));
}

app.UseHttpsRedirection();

app.UseMiddleware<RateLimitMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.MapControllers();

app.Run();

/// <summary>Ponto de entrada usado pelos testes de integração (WebApplicationFactory).</summary>
public partial class Program;
