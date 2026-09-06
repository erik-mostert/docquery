using DocQuery.Application.Documents.Listing;

namespace DocQuery.Query.Api.Endpoints.Documents;

/// <summary>A document's processing state. <paramref name="Status"/> is one of Uploaded, Chunked, Embedded, Failed.</summary>
public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    long SizeInBytes,
    string Status,
    DateTimeOffset UploadedAt,
    int? ChunkCount,
    DateTimeOffset? ChunkedAt,
    DateTimeOffset? EmbeddedAt,
    string? FailureReason)
{
    public static DocumentResponse From(DocumentSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new DocumentResponse(
            summary.Id.Value,
            summary.FileName,
            summary.SizeInBytes,
            summary.Status.ToString(),
            summary.UploadedAt,
            summary.ChunkCount,
            summary.ChunkedAt,
            summary.EmbeddedAt,
            summary.FailureReason);
    }
}
