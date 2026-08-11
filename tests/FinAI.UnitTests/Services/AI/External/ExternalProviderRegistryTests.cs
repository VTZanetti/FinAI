using FinAI.Api.Services.AI.External;
using FluentAssertions;

namespace FinAI.UnitTests.Services.AI.External;

[Trait("Category", "Unit")]
public class ExternalProviderRegistryTests
{
    private readonly ExternalProviderRegistry _registry = new();

    [Fact]
    public void Upsert_And_Get_ReturnsConfig()
    {
        // Arrange
        var config = new ExternalProviderConfig
        {
            Name = "openai-gpt4o",
            Type = ExternalProviderType.OpenAI,
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-4o-mini",
            ApiKeyEnvVar = "OPENAI_API_KEY"
        };

        // Act
        _registry.Upsert(config);
        var result = _registry.Get("openai-gpt4o");

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public void Get_UnknownProvider_ReturnsNull()
    {
        // Act
        var result = _registry.Get("nao-existe");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Remove_DeletesProvider()
    {
        // Arrange
        _registry.Upsert(new ExternalProviderConfig { Name = "x", Type = ExternalProviderType.Custom });

        // Act
        var removed = _registry.Remove("x");
        var result = _registry.Get("x");

        // Assert
        removed.Should().BeTrue();
        result.Should().BeNull();
    }

    [Fact]
    public void ToSafeDto_NeverExposesApiKey()
    {
        // Arrange
        var config = new ExternalProviderConfig
        {
            Name = "secret-provider",
            Type = ExternalProviderType.OpenAI,
            BaseUrl = "https://x",
            Model = "m",
            ApiKeyEnvVar = "OPENAI_API_KEY"
        };

        // Act
        var dto = config.ToSafeDto();
        var json = System.Text.Json.JsonSerializer.Serialize(dto);

        // Assert: apenas o nome da env var, nunca o valor
        json.Should().Contain("OPENAI_API_KEY");
        json.Should().NotContain("sk-");
    }
}
