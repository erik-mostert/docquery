using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Upload;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Application.Tests.Documents.Upload;

public sealed class UploadDocumentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] PdfBytes = "%PDF-1.4 test"u8.ToArray();

    private readonly FakeBlobStore _blobStore = new();
    private readonly InMemoryDocumentRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ICommandHandler<UploadDocumentCommand, DocumentId> _handler;

    public UploadDocumentHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton<IBlobStore>(_blobStore);
        services.AddSingleton<IDocumentRepository>(_repository);
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));

        _handler = services.BuildServiceProvider().GetRequiredService<ICommandHandler<UploadDocumentCommand, DocumentId>>();
    }

    [Fact]
    public async Task Uploads_blob_named_after_document_id()
    {
        using var content = new MemoryStream(PdfBytes);

        var id = await _handler.HandleAsync(Command(content), CancellationToken.None);

        var upload = Assert.Single(_blobStore.Uploads);
        Assert.Equal(FormattableString.Invariant($"{id.Value}.pdf"), upload.BlobName);
        Assert.Equal("application/pdf", upload.ContentType);
        Assert.Equal(PdfBytes.Length, upload.Length);
    }

    [Fact]
    public async Task Adds_document_and_saves_once()
    {
        using var content = new MemoryStream(PdfBytes);

        var id = await _handler.HandleAsync(Command(content), CancellationToken.None);

        var document = Assert.Single(_repository.Documents);
        Assert.Equal(id, document.Id);
        Assert.Equal("report.pdf", document.FileName);
        Assert.Equal(Now, document.UploadedAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rejects_non_pdf_before_touching_storage()
    {
        using var content = new MemoryStream(PdfBytes);
        var command = Command(content) with { ContentType = "text/plain" };

        await Assert.ThrowsAsync<DomainException>(() => _handler.HandleAsync(command, CancellationToken.None));

        Assert.Empty(_blobStore.Uploads);
        Assert.Empty(_repository.Documents);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rejects_null_command()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _handler.HandleAsync(null!, CancellationToken.None));
    }

    private static UploadDocumentCommand Command(Stream content) =>
        new("report.pdf", "application/pdf", PdfBytes.Length, content);
}
