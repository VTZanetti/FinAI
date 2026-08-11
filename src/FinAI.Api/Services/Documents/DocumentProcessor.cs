using System.Threading.Channels;
using FinAI.Api.Models;
using FinAI.Api.Repositories;
using FinAI.Api.Services;
using FinAI.Api.Services.AI;
using Microsoft.Extensions.Options;
using Pgvector;

namespace FinAI.Api.Services.Documents;

/// <summary>
/// Pipeline assíncrono de processamento de documentos: extração → chunking → embeddings → armazenamento.
/// </summary>
public interface IDocumentProcessor
{
    void Enqueue(Guid documentId, Guid userId);
}

/// <summary>
/// No-op processor para ambientes sem processamento async (testes) — o documento permanece "Processing".
/// </summary>
public class NoopDocumentProcessor : IDocumentProcessor
{
    public void Enqueue(Guid documentId, Guid userId)
    {
        // Não faz nada — pipeline desligado
    }
}

/// <summary>
/// Processador em background (Channel simples) — o plano prevê HostedService/Channel em dev.
/// </summary>
public class DocumentProcessor : BackgroundService, IDocumentProcessor
{
    private readonly Channel<(Guid DocumentId, Guid UserId)> _queue = Channel.CreateUnbounded<(Guid, Guid)>();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(IServiceScopeFactory scopeFactory, ILogger<DocumentProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(Guid documentId, Guid userId)
    {
        // Se o processamento está desligado (testes), ignora a fila — o documento fica "Processing"
        using var scope = _scopeFactory.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DocumentOptions>>().Value;
        if (!options.ProcessingEnabled)
            return;

        _queue.Writer.TryWrite((documentId, userId));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (documentId, userId) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Processamento em background NÃO usa o stoppingToken do request —
                // usa CancellationToken.None para não ser cancelado indevidamente.
                await ProcessAsync(documentId, userId, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Document processor stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document processing failed for {DocumentId}", documentId);
            }
        }
    }

    /// <summary>
    /// Obtém embeddings por chunk. Se o provider retornou lista de vetores, usa; senão gera um a um.
    /// </summary>
    private static async Task<float[][]> GetEmbeddingsPerChunkAsync(
        IEmbeddingService embeddings,
        IReadOnlyList<ChunkResult> chunks,
        EmbeddingResult firstResult,
        CancellationToken cancellationToken)
    {
        // O provider atual retorna apenas o primeiro vetor; gera individualmente para cada chunk
        var result = new float[chunks.Count][];
        result[0] = firstResult.Values;

        for (var i = 1; i < chunks.Count; i++)
        {
            var single = await embeddings.EmbedAsync(chunks[i].Content, cancellationToken);
            if (!single.Success || single.Values.Length == 0)
                throw new InvalidOperationException($"Embedding generation failed for chunk {i}: {single.Error}");
            result[i] = single.Values;
        }

        return result;
    }

    internal async Task ProcessAsync(Guid documentId, Guid userId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var extractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
        var chunker = scope.ServiceProvider.GetRequiredService<IChunker>();
        var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DocumentOptions>>().Value;

        var document = await documents.GetByIdAsync(documentId, userId, cancellationToken);
        if (document is null || document.Status != DocumentStatus.Processing)
            return;

        try
        {
            if (!File.Exists(document.StorageUri))
                throw new FileNotFoundException($"Document file not found: {document.StorageUri}");

            // 1. Extração de texto
            await using var stream = File.OpenRead(document.StorageUri);
            var text = await extractor.ExtractAsync(stream, document.ContentType, cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("No text could be extracted from the document");

            // 2. Chunking
            var chunks = chunker.Chunk(text, options.ChunkTokens, options.ChunkOverlapTokens);
            if (chunks.Count == 0)
                throw new InvalidOperationException("Document produced no chunks");

            // 3. Embeddings (batch — o Ollama /api/embed retorna um vetor por input)
            var embeddingResult = await embeddings.EmbedBatchAsync(chunks.Select(c => c.Content).ToList(), cancellationToken);
            if (!embeddingResult.Success || embeddingResult.Values.Length == 0)
                throw new InvalidOperationException($"Embedding generation failed: {embeddingResult.Error}");

            // 3b. Se o provider retornou apenas um vetor (modo fallback), repete para todos os chunks
            var embeddingsBatch = chunks.Count == 1
                ? new[] { embeddingResult.Values }
                : await GetEmbeddingsPerChunkAsync(embeddings, chunks, embeddingResult, cancellationToken);

            // 4. Armazenar chunks + vetores
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    ChunkIndex = i,
                    Content = chunks[i].Content,
                    Embedding = new Vector(embeddingsBatch[i]),
                    Tokens = chunks[i].Tokens
                };
                await vectorStore.UpsertChunkAsync(chunk, cancellationToken);
            }

            // 5. Status ready
            document.Status = DocumentStatus.Ready;
            document.TextLength = text.Length;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            documents.Update(document);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document {DocumentId} processed: {Chunks} chunks, {Chars} chars", documentId, chunks.Count, text.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document processing failed for {DocumentId}", documentId);
            document.Status = DocumentStatus.Failed;
            document.FailureReason = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            documents.Update(document);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }
}
