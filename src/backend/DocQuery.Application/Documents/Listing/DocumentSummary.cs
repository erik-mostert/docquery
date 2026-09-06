using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Listing;

/// <summary>Read model of a document's processing state, as shown in the UI.</summary>
public sealed record DocumentSummary(
    DocumentId Id,
    string FileName,
    long SizeInBytes,
    DocumentStatus Status,
    DateTimeOffset UploadedAt,
    int? ChunkCount,
    DateTimeOffset? ChunkedAt,
    DateTimeOffset? EmbeddedAt,
    string? FailureReason)
{
    public static DocumentSummary From(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new DocumentSummary(
            document.Id,
            document.FileName,
            document.SizeInBytes,
            document.Status,
            document.UploadedAt,
            document.ChunkCount,
            document.ChunkedAt,
            document.EmbeddedAt,
            document.FailureReason);
    }
}
