using System.Text.Json.Serialization;
using FinAI.Api.Data;
using FinAI.Api.Middleware;
using FinAI.Api.Repositories;
using FinAI.Api.Security;
using FinAI.Api.Services;
using FinAI.Api.Services.Accounts;
using FinAI.Api.Services.Budgets;
using FinAI.Api.Services.Categories;
using FinAI.Api.Services.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.Configure<RouteOptions>(o => o.LowercaseUrls = true);

// ── EF Core + PostgreSQL ───────────────────────────────────────────────────
builder.Services.AddDbContext<FinAiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── DI: Security ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ICurrentUser, DevCurrentUser>();

// ── DI: Repositories ───────────────────────────────────────────────────────
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBudgetRepository, BudgetRepository>();

// ── DI: Services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();

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
});

// ── Health checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FinAiDbContext>("postgres");

var app = builder.Build();

// ── Pipeline ───────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinAI API v1"));
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.MapControllers();

app.Run();

/// <summary>Ponto de entrada usado pelos testes de integração (WebApplicationFactory).</summary>
public partial class Program;
