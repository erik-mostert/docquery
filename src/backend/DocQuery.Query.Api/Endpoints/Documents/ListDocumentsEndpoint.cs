using DocQuery.Api.Common.Endpoints;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Listing;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocQuery.Query.Api.Endpoints.Documents;

/// <summary><c>GET /documents?limit=</c>: the most recent documents, newest first, for the UI's document list.</summary>
internal sealed class ListDocumentsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/documents", HandleAsync)
            .WithName("ListDocuments")
            .WithTags("Documents")
            .WithSummary("List the most recently uploaded documents with their processing state.");
    }

    private static async Task<Ok<IReadOnlyList<DocumentResponse>>> HandleAsync(
        int? limit,
        IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>> handler,
        CancellationToken cancellationToken)
    {
        var summaries = await handler.HandleAsync(new ListDocumentsQuery(limit), cancellationToken);

        IReadOnlyList<DocumentResponse> response = summaries.Select(DocumentResponse.From).ToList();
        return TypedResults.Ok(response);
    }
}
