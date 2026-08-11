namespace FinAI.Api.Services.Documents;

public sealed record ChunkResult(string Content, int Tokens);

public interface IChunker
{
    IReadOnlyList<ChunkResult> Chunk(string text, int maxTokens, int overlapTokens);
}

/// <summary>
/// Chunking por palavras com overlap (aproximação de tokens: 1 token ≈ 1 palavra).
/// </summary>
public class TokenChunker : IChunker
{
    public IReadOnlyList<ChunkResult> Chunk(string text, int maxTokens, int overlapTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return [];

        if (words.Length <= maxTokens)
            return [new ChunkResult(text.Trim(), words.Length)];

        var chunks = new List<ChunkResult>();
        var position = 0;

        while (position < words.Length)
        {
            var take = Math.Min(maxTokens, words.Length - position);
            var chunkWords = words.Skip(position).Take(take).ToArray();
            chunks.Add(new ChunkResult(string.Join(' ', chunkWords), chunkWords.Length));

            if (position + take >= words.Length)
                break;

            // Avança deixando overlap
            position += take - overlapTokens;
        }

        return chunks;
    }
}
