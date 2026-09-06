using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Listing;

/// <summary>One document's processing state; the handler returns null when it does not exist.</summary>
public sealed record GetDocumentQuery(DocumentId Id);
