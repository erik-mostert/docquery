using DocQuery.Application.Abstractions;
using DocQuery.Domain.Common;

namespace DocQuery.Application.Documents.Listing;

internal sealed class ListDocumentsHandler(IDocumentRepository documents) : IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>>
{
    public async Task<IReadOnlyList<DocumentSummary>> HandleAsync(ListDocumentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = query.Limit ?? ListDocumentsQuery.DefaultLimit;
        if (limit < 1 || limit > ListDocumentsQuery.MaxLimit)
        {
            throw new DomainException(FormattableString.Invariant($"Limit must be between 1 and {ListDocumentsQuery.MaxLimit}."));
        }

        var listed = await documents.ListAsync(limit, cancellationToken);

        return listed.Select(DocumentSummary.From).ToList();
    }
}
