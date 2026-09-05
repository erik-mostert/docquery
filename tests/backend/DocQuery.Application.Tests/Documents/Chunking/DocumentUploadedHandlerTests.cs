using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Chunking;
using DocQuery.Application.Exceptions;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocQuery.Application.Tests.Documents.Chunking;

public sealed class DocumentUploadedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly byte[] PdfBytes = "%PDF-1.4 fake"u8.ToArray();

    private readonly InMemoryDocumentRepository _documents = new();
    private readonly FakeBlobStore _blobStore = new();
    private readonly FakePdfTextExtractor _extractor = new();
    private readonly FakeDocumentChunkRepository _chunks = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly IIntegrationMessageHandler<DocumentUploaded> _handler;

    public DocumentUploadedHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddChunking();
        services.AddSingleton<IDocumentRepository>(_documents);
        services.AddSingleton<IBlobStore>(_blobStore);
        services.AddSingleton<IPdfTextExtractor>(_extractor);
        services.AddSingleton<ITokenizer>(new FakeTokenizer());
        services.AddSingleton<IDocumentChunkRepository>(_chunks);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSingleton(Options.Create(new ChunkingOptions { MaxTokens = 50, OverlapTokens = 5 }));

        _handler = services.BuildServiceProvider().GetRequiredService<IIntegrationMessageHandler<DocumentUploaded>>();
    }

    [Fact]
    public async Task Chunks_the_document_marks_it_chunked_and_saves_once()
    {
        var document = await SeedUploadedDocumentAsync();
        _extractor.Pages.Add(new PageText(1, [new TextBlock(["First paragraph of text."]), new TextBlock(["Second paragraph of text."])]));

        await _handler.HandleAsync(Message(document), CancellationToken.None);

        Assert.Equal(DocumentStatus.Chunked, document.Status);
        var stored = _chunks.ChunksFor(document.Id);
        Assert.NotEmpty(stored);
        Assert.Equal(stored.Count, document.ChunkCount);
        Assert.Equal(Now, document.ChunkedAt);
        Assert.Contains(document.DomainEvents, e => e is DocumentChunkedEvent);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(1, _blobStore.DownloadCount);
    }

    [Fact]
    public async Task Redelivery_for_an_already_chunked_document_is_ignored_without_downloading()
    {
        var document = await SeedUploadedDocumentAsync();
        document.MarkChunked(3, Now);

        await _handler.HandleAsync(Message(document), CancellationToken.None);

        Assert.Equal(0, _blobStore.DownloadCount);
        Assert.Equal(0, _extractor.CallCount);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unknown_document_is_a_permanent_failure_with_nothing_saved()
    {
        var message = new DocumentUploaded(Guid.NewGuid(), "missing.pdf", Now);

        await Assert.ThrowsAsync<PermanentProcessingException>(() => _handler.HandleAsync(message, CancellationToken.None));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Missing_blob_marks_the_document_failed_and_rethrows_as_permanent()
    {
        var document = await SeedUploadedDocumentAsync(seedBlob: false);

        var exception = await Assert.ThrowsAsync<PermanentProcessingException>(() => _handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Contains(document.BlobName, document.FailureReason, StringComparison.Ordinal);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.IsType<BlobNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task Unparseable_pdf_marks_the_document_failed_and_rethrows()
    {
        var document = await SeedUploadedDocumentAsync();
        _extractor.FailWith(new PermanentProcessingException("PDF could not be parsed"));

        var exception = await Assert.ThrowsAsync<PermanentProcessingException>(() => _handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal("PDF could not be parsed", exception.Message);
        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("PDF could not be parsed", document.FailureReason);
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal(0, _chunks.ReplaceCount);
    }

    [Fact]
    public async Task Document_without_extractable_text_is_marked_failed()
    {
        var document = await SeedUploadedDocumentAsync();
        _extractor.Pages.Add(new PageText(1, []));

        await Assert.ThrowsAsync<PermanentProcessingException>(() => _handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Contains("no extractable text", document.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _chunks.ReplaceCount);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unexpected_failures_propagate_without_marking_the_document_failed()
    {
        var document = await SeedUploadedDocumentAsync();
        _extractor.FailWith(new IOException("disk hiccup"));

        await Assert.ThrowsAsync<IOException>(() => _handler.HandleAsync(Message(document), CancellationToken.None));

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private async Task<Document> SeedUploadedDocumentAsync(bool seedBlob = true)
    {
        var document = Document.Upload("report.pdf", "application/pdf", PdfBytes.Length, Now.AddMinutes(-5));
        document.ClearDomainEvents();
        await _documents.AddAsync(document, CancellationToken.None);
        if (seedBlob)
        {
            _blobStore.Seed(document.BlobName, PdfBytes);
        }

        return document;
    }

    private static DocumentUploaded Message(Document document) => new(document.Id.Value, document.BlobName, document.UploadedAt);
}
