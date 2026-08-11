using FinAI.Api.Services.AI;
using FluentAssertions;

namespace FinAI.UnitTests.Services.AI;

[Trait("Category", "Unit")]
public class RuleClassifierTests
{
    private readonly RuleClassifier _classifier = new();

    [Fact]
    public void Match_UberDescription_ReturnsTransportationRideSharing()
    {
        // Act
        var result = _classifier.Match("UBER *TRIP 09/08");

        // Assert
        result.Should().NotBeNull();
        result!.Category.Should().Be("Transportation");
        result.Subcategory.Should().Be("Ride Sharing");
        result.Source.Should().Be("rules");
        result.Confidence.Should().Be(0.85m);
    }

    [Fact]
    public void Match_IFoodDescription_ReturnsFoodRestaurant()
    {
        // Act
        var result = _classifier.Match("ifood *pedido 123");

        // Assert: normalização uppercase
        result.Should().NotBeNull();
        result!.Category.Should().Be("Food");
        result.Subcategory.Should().Be("Restaurant");
    }

    [Fact]
    public void Match_AccentedDescription_NormalizesAndMatches()
    {
        // Act: "pão de açúcar" com acentos
        var result = _classifier.Match("PÃO DE AÇÚCAR - MERCADO");

        // Assert: acento removido na normalização
        result.Should().NotBeNull();
        result!.Category.Should().Be("Food");
        result.Subcategory.Should().Be("Groceries");
    }

    [Fact]
    public void Match_UnknownDescription_ReturnsNull()
    {
        // Act
        var result = _classifier.Match("TRANSFERENCIA PIX JOAO");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Match_NetflixDescription_ReturnsEntertainmentStreaming()
    {
        // Act
        var result = _classifier.Match("NETFLIX.COM");

        // Assert
        result.Should().NotBeNull();
        result!.Category.Should().Be("Entertainment");
        result.Subcategory.Should().Be("Streaming");
    }

    [Fact]
    public void Match_EmptyDescription_ReturnsNull()
    {
        // Act
        var result = _classifier.Match("");

        // Assert
        result.Should().BeNull();
    }
}
