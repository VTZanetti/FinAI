using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.AI;
using FinAI.Api.Services.Documents;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinAI.UnitTests.Services.Documents;

[Trait("Category", "Unit")]
public class DocumentProcessorTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Process_FullPipeline_MarksDocumentReady()
    {
        // Arrange
        var document = new FinancialDocument
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FileName = "extrato.txt",
            ContentType = "text/plain",
            StorageUri = Path.Combine(Path.GetTempPath(), "finai-proc.txt"),
            Status = DocumentStatus.Processing
        };
        File.WriteAllText(document.StorageUri, "Extrato bancário junho. " + string.Join(' ', Enumerable.Repeat("gasto", 100)));

        var docsRepo = Substitute.For<IDocumentRepository>();
        docsRepo.GetByIdAsync(document.Id, UserId, Arg.Any<CancellationToken>()).Returns(document);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var extractor = Substitute.For<ITextExtractor>();
        extractor.ExtractAsync(Arg.Any<Stream>(), "text/plain", Arg.Any<CancellationToken>())
            .Returns(c => File.ReadAllText(document.StorageUri));

        var embeddings = Substitute.For<IEmbeddingService>();
        embeddings.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult(Enumerable.Repeat(0.01f, 768).ToArray(), true));
        embeddings.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResult(Enumerable.Repeat(0.01f, 768).ToArray(), true));

        var vectorStore = Substitute.For<IVectorStore>();

        var services = new ServiceCollection();
        services.AddSingleton(docsRepo);
        services.AddSingleton(unitOfWork);
        services.AddSingleton(extractor);
        services.AddSingleton<IChunker>(new TokenChunker());
        services.AddSingleton(embeddings);
        services.AddSingleton(vectorStore);
        services.AddSingleton(Options.Create(new DocumentOptions { ChunkTokens = 50, ChunkOverlapTokens = 10 }));
        var provider = services.BuildServiceProvider();

        var processor = new DocumentProcessor(
            new DefaultServiceScopeFactory(provider),
            NullLogger<DocumentProcessor>.Instance);

        // Act
        await processor.ProcessAsync(document.Id, UserId, CancellationToken.None);

        // Assert
        document.Status.Should().Be(DocumentStatus.Ready);
        document.TextLength.Should().BeGreaterThan(0);
        await vectorStore.Received().UpsertChunkAsync(Arg.Any<DocumentChunk>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received().SaveChangesAsync(Arg.Any<CancellationToken>());

        File.Delete(document.StorageUri);
    }

    [Fact]
    public async Task Process_ExtractionFails_MarksDocumentFailed()
    {
        // Arrange
        var document = new FinancialDocument
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FileName = "quebrado.txt",
            ContentType = "text/plain",
            StorageUri = Path.Combine(Path.GetTempPath(), "finai-quebrado.txt"),
            Status = DocumentStatus.Processing
        };
        File.WriteAllText(document.StorageUri, "x");

        var docsRepo = Substitute.For<IDocumentRepository>();
        docsRepo.GetByIdAsync(document.Id, UserId, Arg.Any<CancellationToken>()).Returns(document);

        var extractor = Substitute.For<ITextExtractor>();
        extractor.ExtractAsync(Arg.Any<Stream>(), "text/plain", Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("Falha na extração"));

        var services = new ServiceCollection();
        services.AddSingleton(docsRepo);
        services.AddSingleton(Substitute.For<IUnitOfWork>());
        services.AddSingleton(extractor);
        services.AddSingleton<IChunker>(new TokenChunker());
        services.AddSingleton(Substitute.For<IEmbeddingService>());
        services.AddSingleton(Substitute.For<IVectorStore>());
        services.AddSingleton(Options.Create(new DocumentOptions()));
        var provider = services.BuildServiceProvider();

        var processor = new DocumentProcessor(
            new DefaultServiceScopeFactory(provider),
            NullLogger<DocumentProcessor>.Instance);

        // Act
        await processor.ProcessAsync(document.Id, UserId, CancellationToken.None);

        // Assert
        document.Status.Should().Be(DocumentStatus.Failed);
        document.FailureReason.Should().NotBeNullOrWhiteSpace();

        File.Delete(document.StorageUri);
    }

    private sealed class DefaultServiceScopeFactory : IServiceScopeFactory
    {
        private readonly ServiceProvider _provider;

        public DefaultServiceScopeFactory(ServiceProvider provider) => _provider = provider;

        public IServiceScope CreateScope() => _provider.CreateScope();
    }
}
