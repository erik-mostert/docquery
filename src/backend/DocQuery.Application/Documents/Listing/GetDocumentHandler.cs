using DocQuery.Application.Abstractions;

namespace DocQuery.Application.Documents.Listing;

internal sealed class GetDocumentHandler(IDocumentRepository documents) : IQueryHandler<GetDocumentQuery, DocumentSummary?>
{
    public async Task<DocumentSummary?> HandleAsync(GetDocumentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var document = await documents.GetByIdAsync(query.Id, cancellationToken);

        return document is null ? null : DocumentSummary.From(document);
    }
}
