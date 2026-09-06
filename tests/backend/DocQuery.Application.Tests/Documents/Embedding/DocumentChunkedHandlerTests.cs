using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Embedding;
using DocQuery.Application.Exceptions;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocQuery.Application.Tests.Documents.Embedding;

public sealed class DocumentChunkedHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDocumentRepository _documents = new();
    private readonly FakeDocumentChunkRepository _chunks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private FakeEmbeddingGenerator _generator = new();

    [Fact]
    public async Task Embeds_every_chunk_in_batches_and_marks_the_document_embedded()
    {
        var document = await SeedChunkedDocumentAsync(chunkCount: 5);
        var handler = BuildHandler(batchSize: 2);

        await handler.HandleAsync(Message(document), CancellationToken.None);

        Assert.Equal([2, 2, 1], _generator.Batches.Select(batch => batch.Count).ToArray());
        Assert.All(_chunks.ChunksFor(document.Id), chunk => Assert.True(chunk.HasEmbedding));
        Assert.Equal(DocumentStatus.Embedded, document.Status);
        Assert.Equal(Now, document.EmbeddedAt);
        Assert.Contains(document.DomainEvents, e => e is DocumentEmbeddedEvent);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Chunk_embeddings_come_from_the_generator_in_order()
    {
        var document = await SeedChunkedDocumentAsync(chunkCount: 3);
        var handler = BuildHandler(batchSize: 10);

        await handler.HandleAsync(Message(document), CancellationToken.None);

        foreach (var chunk in _chunks.ChunksFor(document.Id))
        {
            Assert.Equal(_generator.Vector(chunk.Text), chunk.Embedding!.Value.ToArray());
        }
    }

    [Fact]
    public async Task Chunks_that_already_have_embeddings_are_skipped()
    {
        var document = await SeedChunkedDocumentAsync(chunkCount: 3);
        _chunks.ChunksFor(document.Id)[1].SetEmbedding(new float[DocumentChunk.EmbeddingDimensions]);
        var handler = BuildHandler(batchSize: 10);

        await handler.HandleAsync(Message(document), CancellationToken.None);

        var batch = Assert.Single(_generator.Batches);
        Assert.Equal(2, batch.Count);
        Assert.Equal(DocumentStatus.Embedded, document.Status);
    }

    [Fact]
    public async Task Document_that_cannot_be_embedded_is_ignored()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 10, Now);
        await _documents.AddAsync(document, CancellationToken.None);
        var handler = BuildHandler(batchSize: 10);

        await handler.HandleAsync(new DocumentChunked(document.Id.Value, 0, Now), CancellationToken.None);

        Assert.Empty(_generator.Batches);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unknown_document_is_a_permanent_failure_with_nothing_saved()
    {
        var handler = BuildHandler(batchSize: 10);

        await Assert.ThrowsAsync<PermanentProcessingException>(
            () => handler.HandleAsync(new DocumentChunked(Guid.NewGuid(), 1, Now), CancellationToken.None));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Document_without_stored_chunks_is_marked_failed()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 10, Now);
        document.MarkChunked(3, Now);
        document.ClearDomainEvents();
        await _documents.AddAsync(document, CancellationToken.None);
        var handler = BuildHandler(batchSize: 10);

        await Assert.ThrowsAsync<PermanentProcessingException>(() => handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Contains("no chunks", document.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Wrong_vector_size_is_a_permanent_failure_that_marks_the_document_failed()
    {
        var document = await SeedChunkedDocumentAsync(chunkCount: 2);
        _generator = new FakeEmbeddingGenerator(dimensions: 8);
        var handler = BuildHandler(batchSize: 10);

        var exception = await Assert.ThrowsAsync<PermanentProcessingException>(() => handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Contains("1536", exception.Message, StringComparison.Ordinal);
        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Wrong_vector_count_is_a_permanent_failure()
    {
        var document = await SeedChunkedDocumentAsync(chunkCount: 3);
        _generator.ReturnCountOverride = 2;
        var handler = BuildHandler(batchSize: 10);

        await Assert.ThrowsAsync<PermanentProcessingException>(() => handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal(DocumentStatus.Failed, document.Status);
    }

    [Fact]
    public async Task Transient_provider_failure_propagates_without_marking_the_document_failed()
    {
        var document = await SeedChunkedDocumentAsync(chunkCount: 2);
        _generator.FailNextCallWith(new HttpRequestException("429 Too Many Requests"));
        var handler = BuildHandler(batchSize: 10);

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal(DocumentStatus.Chunked, document.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    public void Dispose() => _generator.Dispose();

    private IIntegrationMessageHandler<DocumentChunked> BuildHandler(int batchSize)
    {
        var services = new ServiceCollection();
        services.AddEmbedding();
        services.AddSingleton<IDocumentRepository>(_documents);
        services.AddSingleton<IDocumentChunkRepository>(_chunks);
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_generator);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSingleton(Options.Create(new EmbeddingOptions { BatchSize = batchSize }));

        return services.BuildServiceProvider().GetRequiredService<IIntegrationMessageHandler<DocumentChunked>>();
    }

    private async Task<Document> SeedChunkedDocumentAsync(int chunkCount)
    {
        var document = Document.Upload("report.pdf", "application/pdf", 10, Now.AddMinutes(-2));
        document.MarkChunked(chunkCount, Now.AddMinutes(-1));
        document.ClearDomainEvents();
        await _documents.AddAsync(document, CancellationToken.None);
        _chunks.Seed(document.Id, Enumerable.Range(0, chunkCount).Select(i =>
            DocumentChunk.Create(document.Id, i, pageNumber: 1, text: FormattableString.Invariant($"chunk number {i}"), tokenCount: 3)));

        return document;
    }

    private static DocumentChunked Message(Document document) => new(document.Id.Value, document.ChunkCount ?? 0, document.ChunkedAt ?? Now);
}
