namespace FinAI.Api.Services.Documents;

public sealed class DocumentOptions
{
    public const string SectionName = "Documents";

    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } = ["application/pdf", "text/plain"];
    public int ChunkTokens { get; set; } = 1000;
    public int ChunkOverlapTokens { get; set; } = 150;
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public int SearchTopK { get; set; } = 5;
    public double SearchMinScore { get; set; } = 0.5;
    public string StoragePath { get; set; } = "storage/documents";
    public bool ProcessingEnabled { get; set; } = true;
}
