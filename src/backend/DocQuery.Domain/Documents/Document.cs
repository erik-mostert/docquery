using DocQuery.Domain.Common;

namespace DocQuery.Domain.Documents;

/// <summary>An uploaded PDF and its processing state.</summary>
public sealed class Document : AggregateRoot
{
    public const string PdfContentType = "application/pdf";

    /// <summary>Column limit for <see cref="FailureReason"/>; longer reasons are truncated so a failure can always be recorded.</summary>
    public const int MaxFailureReasonLength = 1000;

    private Document(
        DocumentId id,
        string fileName,
        string contentType,
        long sizeInBytes,
        string blobName,
        DocumentStatus status,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        BlobName = blobName;
        Status = status;
        UploadedAt = uploadedAt;
    }

    public DocumentId Id { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public long SizeInBytes { get; }

    /// <summary>Deterministic blob name derived from the id, so a retried upload overwrites rather than duplicates.</summary>
    public string BlobName { get; }

    public DocumentStatus Status { get; private set; }

    public DateTimeOffset UploadedAt { get; }

    public int? ChunkCount { get; private set; }

    public DateTimeOffset? ChunkedAt { get; private set; }

    public DateTimeOffset? EmbeddedAt { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>Chunking is allowed for fresh uploads and for retries after a failure, never for documents already past chunking.</summary>
    public bool CanBeChunked => Status is DocumentStatus.Uploaded or DocumentStatus.Failed;

    /// <summary>Embedding needs chunks: a chunked document, or a failed one whose chunks survived the failure.</summary>
    public bool CanBeEmbedded => Status == DocumentStatus.Chunked || (Status == DocumentStatus.Failed && ChunkCount > 0);

    public static Document Upload(string fileName, string contentType, long sizeInBytes, DateTimeOffset uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("A file name is required.");
        }

        if (!string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Only PDF documents are supported.");
        }

        if (sizeInBytes <= 0)
        {
            throw new DomainException("The document must not be empty.");
        }

        var id = DocumentId.Create();
        var blobName = FormattableString.Invariant($"{id.Value}.pdf");
        var document = new Document(id, fileName, PdfContentType, sizeInBytes, blobName, DocumentStatus.Uploaded, uploadedAt);

        document.Raise(new DocumentUploadedEvent(id, blobName, uploadedAt));

        return document;
    }

    public void MarkChunked(int chunkCount, DateTimeOffset chunkedAt)
    {
        if (!CanBeChunked)
        {
            throw new DomainException(FormattableString.Invariant($"A document in status {Status} cannot be chunked."));
        }

        if (chunkCount <= 0)
        {
            throw new DomainException("A chunked document must have at least one chunk.");
        }

        Status = DocumentStatus.Chunked;
        ChunkCount = chunkCount;
        ChunkedAt = chunkedAt;
        FailureReason = null;

        Raise(new DocumentChunkedEvent(Id, chunkCount, chunkedAt));
    }

    public void MarkEmbedded(DateTimeOffset embeddedAt)
    {
        if (!CanBeEmbedded)
        {
            throw new DomainException(FormattableString.Invariant($"A document in status {Status} cannot be embedded."));
        }

        Status = DocumentStatus.Embedded;
        EmbeddedAt = embeddedAt;
        FailureReason = null;

        Raise(new DocumentEmbeddedEvent(Id, embeddedAt));
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A failure reason is required.");
        }

        Status = DocumentStatus.Failed;
        FailureReason = reason.Length <= MaxFailureReasonLength ? reason : reason[..MaxFailureReasonLength];
    }
}
