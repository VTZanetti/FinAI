using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services.Audit;
using Microsoft.Extensions.Options;

namespace FinAI.Api.Services.Documents;

public interface IDocumentService
{
    Task<Result<FinancialDocument>> UploadAsync(Guid userId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default);
    Task<Result<FinancialDocument>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<FinancialDocument>>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gerenciamento de documentos: validação, persistência do arquivo em disco e fila de processamento.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _audit;
    private readonly ILogger<DocumentService> _logger;
    private readonly DocumentOptions _options;

    public DocumentService(
        IDocumentRepository documents,
        IUnitOfWork unitOfWork,
        IAuditService audit,
        IOptions<DocumentOptions> options,
        ILogger<DocumentService> logger)
    {
        _documents = documents;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<FinancialDocument>> UploadAsync(Guid userId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        // Validação de tipo (MIME) e tamanho
        if (!_options.AllowedContentTypes.Contains(contentType))
            return Result.Failure<FinancialDocument>(ErrorCode.Validation, $"Content type '{contentType}' is not allowed. Allowed: {string.Join(", ", _options.AllowedContentTypes)}");

        if (content.Length > _options.MaxFileSizeBytes)
            return Result.Failure<FinancialDocument>(ErrorCode.Validation, $"File exceeds maximum size of {_options.MaxFileSizeBytes / 1024 / 1024}MB");

        var id = Guid.NewGuid();
        var storageUri = await SaveFileAsync(id, fileName, content, cancellationToken);

        var document = new FinancialDocument
        {
            Id = id,
            UserId = userId,
            FileName = SanitizeFileName(fileName),
            ContentType = contentType,
            StorageUri = storageUri,
            Status = DocumentStatus.Processing,
            UploadedAt = DateTimeOffset.UtcNow
        };

        await _documents.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync("document.upload", "Document", document.Id, new { fileName = document.FileName }, cancellationToken);

        return Result.Success(document);
    }

    public async Task<Result<FinancialDocument>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdAsync(id, userId, cancellationToken);
        return document is null
            ? Result.Failure<FinancialDocument>(ErrorCode.NotFound, "Document not found")
            : Result.Success(document);
    }

    public async Task<Result<IReadOnlyList<FinancialDocument>>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var documents = await _documents.ListByUserAsync(userId, cancellationToken);
        return Result.Success(documents);
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdAsync(id, userId, cancellationToken);
        if (document is null)
            return Result.Failure(ErrorCode.NotFound, "Document not found");

        _documents.Delete(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        DeleteFile(document.StorageUri);

        await _audit.RecordAsync("document.delete", "Document", document.Id, null, cancellationToken);

        return Result.Success();
    }

    private async Task<string> SaveFileAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Directory.GetCurrentDirectory(), _options.StoragePath);
        Directory.CreateDirectory(directory);

        var safeName = $"{id:N}{Path.GetExtension(fileName)}";
        var path = Path.Combine(directory, safeName);

        await using var fileStream = new FileStream(path, FileMode.Create);
        await content.CopyToAsync(fileStream, cancellationToken);

        return path;
    }

    private void DeleteFile(string storageUri)
    {
        try
        {
            if (File.Exists(storageUri))
                File.Delete(storageUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete document file {Uri}", storageUri);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) ? "document" : name;
    }
}
