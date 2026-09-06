using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Domain.Tests.Documents;

public sealed class DocumentEmbeddingTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 9, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChunkedAt = UploadedAt.AddMinutes(1);
    private static readonly DateTimeOffset EmbeddedAt = UploadedAt.AddMinutes(2);

    [Fact]
    public void Chunked_document_can_be_embedded()
    {
        var document = Chunked();

        Assert.True(document.CanBeEmbedded);
    }

    [Fact]
    public void Uploaded_document_cannot_be_embedded_yet()
    {
        var document = Upload();

        Assert.False(document.CanBeEmbedded);
    }

    [Fact]
    public void Failed_document_with_chunks_can_be_embedded_again()
    {
        var document = Chunked();
        document.MarkFailed("embedding provider rejected the request");

        Assert.True(document.CanBeEmbedded);
    }

    [Fact]
    public void Failed_document_without_chunks_cannot_be_embedded()
    {
        var document = Upload();
        document.MarkFailed("could not parse");

        Assert.False(document.CanBeEmbedded);
    }

    [Fact]
    public void MarkEmbedded_sets_status_time_and_raises_event()
    {
        var document = Chunked();
        document.ClearDomainEvents();

        document.MarkEmbedded(EmbeddedAt);

        Assert.Equal(DocumentStatus.Embedded, document.Status);
        Assert.Equal(EmbeddedAt, document.EmbeddedAt);
        Assert.False(document.CanBeEmbedded);
        Assert.False(document.CanBeChunked);
        var embedded = Assert.IsType<DocumentEmbeddedEvent>(Assert.Single(document.DomainEvents));
        Assert.Equal(document.Id, embedded.DocumentId);
        Assert.Equal(EmbeddedAt, embedded.EmbeddedAt);
    }

    [Fact]
    public void MarkEmbedded_clears_a_previous_failure_reason()
    {
        var document = Chunked();
        document.MarkFailed("transient");

        document.MarkEmbedded(EmbeddedAt);

        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void MarkEmbedded_is_rejected_for_an_uploaded_document()
    {
        var document = Upload();

        Assert.Throws<DomainException>(() => document.MarkEmbedded(EmbeddedAt));
    }

    [Fact]
    public void MarkEmbedded_is_rejected_when_already_embedded()
    {
        var document = Chunked();
        document.MarkEmbedded(EmbeddedAt);

        Assert.Throws<DomainException>(() => document.MarkEmbedded(EmbeddedAt));
    }

    private static Document Upload() => Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);

    private static Document Chunked()
    {
        var document = Upload();
        document.MarkChunked(3, ChunkedAt);
        return document;
    }
}
