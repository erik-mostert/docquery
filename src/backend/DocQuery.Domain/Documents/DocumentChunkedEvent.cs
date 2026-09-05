using DocQuery.Domain.Common;

namespace DocQuery.Domain.Documents;

/// <summary>Raised when a document's text has been split into chunks and they are ready for embedding.</summary>
public sealed record DocumentChunkedEvent(DocumentId DocumentId, int ChunkCount, DateTimeOffset ChunkedAt) : IDomainEvent;
