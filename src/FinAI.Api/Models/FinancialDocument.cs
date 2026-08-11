namespace FinAI.Api.Models;

public enum DocumentStatus
{
    Processing = 1,
    Ready = 2,
    Failed = 3
}

/// <summary>
/// Documento financeiro importado (extrato, fatura, comprovante, contrato) — FR-10.
/// </summary>
public class FinancialDocument
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public int TextLength { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
