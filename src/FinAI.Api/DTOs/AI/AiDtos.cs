namespace FinAI.Api.DTOs.AI;

public sealed record ClassifyRequest(string Description, decimal Amount);

public sealed record ClassifyResponse(
    string Category,
    string? Subcategory,
    decimal Confidence,
    string Source);

public static class AiMappings
{
    public static ClassifyResponse ToResponse(this Services.AI.ClassificationResult r)
        => new(r.Category, r.Subcategory, r.Confidence, r.Source);
}
