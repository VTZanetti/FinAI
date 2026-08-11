using System.Text.Json;
using FinAI.Api.Services.Audit;
using FluentAssertions;

namespace FinAI.UnitTests.Services.Audit;

[Trait("Category", "Unit")]
public class AuditDataSanitizerTests
{
    [Fact]
    public void Sanitize_MasksSensitiveValues()
    {
        // Arrange
        var data = new { amount = 1234.56m, description = "Mercado", password = "segredo" };

        // Act
        var sanitized = AuditDataSanitizer.Sanitize(data);
        var json = JsonSerializer.Serialize(sanitized);

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("amount").GetString().Should().Be("***");
        doc.RootElement.GetProperty("password").GetString().Should().Be("***");
        doc.RootElement.GetProperty("description").GetString().Should().Be("Mercado"); // não sensível
    }

    [Fact]
    public void Sanitize_MasksNestedSensitiveValues()
    {
        // Arrange
        var data = new { transaction = new { amount = -27.90m, type = "Expense" } };

        // Act
        var sanitized = AuditDataSanitizer.Sanitize(data);
        var json = JsonSerializer.Serialize(sanitized);

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("transaction").GetProperty("amount").GetString().Should().Be("***");
        doc.RootElement.GetProperty("transaction").GetProperty("type").GetString().Should().Be("Expense");
    }

    [Fact]
    public void Sanitize_NullReturnsEmptyObject()
    {
        // Act
        var sanitized = AuditDataSanitizer.Sanitize(null!);

        // Assert
        JsonSerializer.Serialize(sanitized).Should().Be("{}");
    }

    [Fact]
    public void Sanitize_KeepsNumbersAndStrings()
    {
        // Arrange
        var data = new { count = 5, name = "Ana" };

        // Act
        var sanitized = AuditDataSanitizer.Sanitize(data);
        var json = JsonSerializer.Serialize(sanitized);

        // Assert
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(5);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Ana");
    }
}
