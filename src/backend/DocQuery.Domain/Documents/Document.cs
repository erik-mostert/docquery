using DocQuery.Domain.Common;

namespace DocQuery.Domain.Documents;

/// <summary>An uploaded PDF and its processing state.</summary>
public sealed class Document : AggregateRoot
{
    public const string PdfContentType = "application/pdf";

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
}
