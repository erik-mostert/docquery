namespace DocQuery.Domain.Documents;

public enum DocumentStatus
{
    /// <summary>Stored in blob storage; waiting to be chunked.</summary>
    Uploaded = 0,

    /// <summary>Text extracted and split into chunks; waiting for embeddings.</summary>
    Chunked = 1,

    /// <summary>Processing failed for a reason that will not go away by retrying; see <c>FailureReason</c>.</summary>
    Failed = 2,

    /// <summary>Every chunk has an embedding; the document can be queried.</summary>
    Embedded = 3,
}
