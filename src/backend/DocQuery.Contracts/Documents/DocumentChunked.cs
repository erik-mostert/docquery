namespace DocQuery.Contracts.Documents;

/// <summary>Published after a document's text has been split into chunks; consumed by the embedding worker.</summary>
public sealed record DocumentChunked(Guid DocumentId, int ChunkCount, DateTimeOffset ChunkedAt);
