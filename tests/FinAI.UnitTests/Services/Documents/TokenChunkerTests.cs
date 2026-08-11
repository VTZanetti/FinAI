using FinAI.Api.Services.Documents;
using FluentAssertions;

namespace FinAI.UnitTests.Services.Documents;

[Trait("Category", "Unit")]
public class TokenChunkerTests
{
    private readonly TokenChunker _chunker = new();

    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        // Arrange
        var text = "Extrato bancário do mês de junho.";

        // Act
        var chunks = _chunker.Chunk(text, maxTokens: 1000, overlapTokens: 150);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Content.Should().Be(text.Trim());
    }

    [Fact]
    public void Chunk_LongText_SplitsWithOverlap()
    {
        // Arrange: texto com 30 palavras, max 10, overlap 3
        var text = string.Join(' ', Enumerable.Range(1, 30).Select(i => $"palavra{i}"));

        // Act
        var chunks = _chunker.Chunk(text, maxTokens: 10, overlapTokens: 3);

        // Assert
        chunks.Count.Should().BeGreaterThan(1);
        // Overlap: as 3 últimas palavras do chunk 1 aparecem no início do chunk 2
        var firstWords = chunks[0].Content.Split(' ');
        var secondWords = chunks[1].Content.Split(' ');
        firstWords.Skip(firstWords.Length - 3).Should().Contain(secondWords.Take(3));
    }

    [Fact]
    public void Chunk_EmptyText_ReturnsEmpty()
    {
        // Act
        var chunks = _chunker.Chunk("   ", maxTokens: 100, overlapTokens: 10);

        // Assert
        chunks.Should().BeEmpty();
    }

    [Fact]
    public void Chunk_ExactMaxTokens_ReturnsSingleChunk()
    {
        // Arrange: exatamente 10 palavras
        var text = string.Join(' ', Enumerable.Range(1, 10).Select(i => $"w{i}"));

        // Act
        var chunks = _chunker.Chunk(text, maxTokens: 10, overlapTokens: 2);

        // Assert
        chunks.Should().HaveCount(1);
        chunks[0].Tokens.Should().Be(10);
    }

    [Fact]
    public void Chunk_AllTokensCounted()
    {
        // Arrange
        var text = string.Join(' ', Enumerable.Range(1, 25).Select(i => $"palavra{i}"));

        // Act
        var chunks = _chunker.Chunk(text, maxTokens: 10, overlapTokens: 5);

        // Assert: a soma dos tokens dos chunks ≥ total (overlap infla)
        chunks.Sum(c => c.Tokens).Should().BeGreaterThanOrEqualTo(25);
    }
}
