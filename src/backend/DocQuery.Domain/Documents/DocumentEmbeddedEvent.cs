using DocQuery.Domain.Common;

namespace DocQuery.Domain.Documents;

/// <summary>Raised when every chunk of a document has an embedding and the document is ready to be queried.</summary>
public sealed record DocumentEmbeddedEvent(DocumentId DocumentId, DateTimeOffset EmbeddedAt) : IDomainEvent;
