# FinAI — Financial Intelligence as a Service

API financeira inteligente em ASP.NET Core: recebe transações e entrega análises, previsões e recomendações com IA.

## Stack

- .NET 8+ / ASP.NET Core, EF Core, PostgreSQL
- Identity + JWT (v0.2+), Serilog, Swagger/OpenAPI
- Docker Compose (API + PostgreSQL + Ollama)

## Requisitos

- Docker Desktop (único pré-requisito de fato)
- .NET SDK 8+ (opcional, para rodar a API fora do Docker)

## Comandos essenciais

```powershell
# Subir infra local (PostgreSQL + Ollama)
docker compose up -d

# Aplicar migrations + seed
dotnet ef database update --project src/FinAI.Api

# Rodar a API (Swagger em http://localhost:5xxx/swagger)
dotnet run --project src/FinAI.Api

# Build e testes
dotnet build
dotnet test
```

## Estrutura

```
src/FinAI.Api/            # Web API
tests/FinAI.UnitTests/    # Testes unitários (xUnit + NSubstitute)
tests/FinAI.IntegrationTests/  # Testes de integração (Testcontainers PostgreSQL)
```
