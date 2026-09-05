using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Domain.Tests.Documents;

public sealed class DocumentChunkingTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChunkedAt = UploadedAt.AddMinutes(1);

    [Fact]
    public void Uploaded_document_can_be_chunked()
    {
        var document = Upload();

        Assert.True(document.CanBeChunked);
    }

    [Fact]
    public void MarkChunked_sets_status_count_time_and_raises_event()
    {
        var document = Upload();
        document.ClearDomainEvents();

        document.MarkChunked(7, ChunkedAt);

        Assert.Equal(DocumentStatus.Chunked, document.Status);
        Assert.Equal(7, document.ChunkCount);
        Assert.Equal(ChunkedAt, document.ChunkedAt);
        Assert.False(document.CanBeChunked);
        var chunked = Assert.IsType<DocumentChunkedEvent>(Assert.Single(document.DomainEvents));
        Assert.Equal(document.Id, chunked.DocumentId);
        Assert.Equal(7, chunked.ChunkCount);
        Assert.Equal(ChunkedAt, chunked.ChunkedAt);
    }

    [Fact]
    public void MarkChunked_rejects_non_positive_count()
    {
        var document = Upload();

        Assert.Throws<DomainException>(() => document.MarkChunked(0, ChunkedAt));
    }

    [Fact]
    public void MarkChunked_is_rejected_when_already_chunked()
    {
        var document = Upload();
        document.MarkChunked(3, ChunkedAt);

        Assert.Throws<DomainException>(() => document.MarkChunked(3, ChunkedAt));
    }

    [Fact]
    public void MarkFailed_sets_status_and_reason()
    {
        var document = Upload();

        document.MarkFailed("PDF could not be parsed");

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal("PDF could not be parsed", document.FailureReason);
        Assert.True(document.CanBeChunked, "a failed document may be retried");
    }

    [Fact]
    public void MarkFailed_truncates_long_reasons_to_the_column_limit()
    {
        var document = Upload();

        document.MarkFailed(new string('x', 5000));

        Assert.Equal(Document.MaxFailureReasonLength, document.FailureReason!.Length);
    }

    [Fact]
    public void MarkFailed_rejects_blank_reason()
    {
        var document = Upload();

        Assert.Throws<DomainException>(() => document.MarkFailed("  "));
    }

    [Fact]
    public void Failed_document_can_be_chunked_again_and_reason_is_cleared()
    {
        var document = Upload();
        document.MarkFailed("transient parse problem");

        document.MarkChunked(2, ChunkedAt);

        Assert.Equal(DocumentStatus.Chunked, document.Status);
        Assert.Null(document.FailureReason);
    }

    private static Document Upload() => Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);
}
