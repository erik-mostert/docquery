using DocQuery.Domain.Common;

namespace DocQuery.Domain.Documents;

/// <summary>Raised when a document has been stored and is ready for chunking.</summary>
public sealed record DocumentUploadedEvent(DocumentId DocumentId, string BlobName, DateTimeOffset UploadedAt) : IDomainEvent;
