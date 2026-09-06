namespace DocQuery.Contracts.Documents;

/// <summary>Published when every chunk of a document has an embedding; the document is ready for querying.</summary>
public sealed record DocumentEmbedded(Guid DocumentId, DateTimeOffset EmbeddedAt);
