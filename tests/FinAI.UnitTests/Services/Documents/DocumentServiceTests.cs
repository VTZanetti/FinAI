using FinAI.Api.Common;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.Audit;
using FinAI.Api.Services.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinAI.UnitTests.Services.Documents;

[Trait("Category", "Unit")]
public class DocumentServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();

    private DocumentService CreateService(DocumentOptions? options = null)
        => new(_documents, _unitOfWork, _audit,
            Options.Create(options ?? new DocumentOptions { StoragePath = Path.Combine(Path.GetTempPath(), "finai-tests") }),
            NullLogger<DocumentService>.Instance);

    [Fact]
    public async Task UploadAsync_InvalidContentType_ReturnsValidationError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.UploadAsync(UserId, "malware.exe", "application/x-msdownload", new MemoryStream());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public async Task UploadAsync_FileTooLarge_ReturnsValidationError()
    {
        // Arrange
        var service = CreateService(new DocumentOptions { MaxFileSizeBytes = 100 });

        // Act: conteúdo de 200 bytes
        var result = await service.UploadAsync(UserId, "grande.txt", "text/plain", new MemoryStream(new byte[200]));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.Validation);
    }

    [Fact]
    public async Task UploadAsync_ValidFile_SavesAsProcessing()
    {
        // Arrange
        var service = CreateService();
        using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("conteúdo do extrato"));

        // Act
        var result = await service.UploadAsync(UserId, "extrato.txt", "text/plain", content);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DocumentStatus.Processing);
        result.Value.UserId.Should().Be(UserId);
        result.Value.ContentType.Should().Be("text/plain");
        await _documents.Received().AddAsync(Arg.Any<FinancialDocument>(), Arg.Any<CancellationToken>());
        await _audit.Received().RecordAsync("document.upload", "Document", result.Value.Id, Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_NotOwned_ReturnsNotFound()
    {
        // Arrange
        _documents.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>()).Returns((FinancialDocument?)null);
        var service = CreateService();

        // Act
        var result = await service.GetByIdAsync(UserId, Guid.NewGuid());

        // Assert: 404 — nunca 403
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ValidDocument_DeletesAndAudits()
    {
        // Arrange
        var doc = new FinancialDocument
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FileName = "x.txt",
            StorageUri = Path.Combine(Path.GetTempPath(), "finai-tests-delete.txt"),
            Status = DocumentStatus.Ready
        };
        File.WriteAllText(doc.StorageUri, "conteúdo");
        _documents.GetByIdAsync(doc.Id, UserId, Arg.Any<CancellationToken>()).Returns(doc);
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(UserId, doc.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _documents.Received().Delete(doc);
        File.Exists(doc.StorageUri).Should().BeFalse(); // arquivo removido
        await _audit.Received().RecordAsync("document.delete", "Document", doc.Id, null, Arg.Any<CancellationToken>());
    }
}
