# Multi-stage build para a API FinAI
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia os projetos (csproj) para cache de restore
COPY FinAI.slnx ./
COPY src/FinAI.Api/*.csproj src/FinAI.Api/
COPY tests/FinAI.UnitTests/*.csproj tests/FinAI.UnitTests/
COPY tests/FinAI.IntegrationTests/*.csproj tests/FinAI.IntegrationTests/
RUN dotnet restore FinAI.slnx

# Copia o código e publica
COPY . .
RUN dotnet publish src/FinAI.Api -c Release -o /app/publish --no-restore

# Runtime stage — usuário não-root
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Usuário não-root (APP_UID definido pela imagem aspnet)
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "FinAI.Api.dll"]
