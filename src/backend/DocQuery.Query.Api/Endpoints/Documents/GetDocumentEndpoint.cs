using DocQuery.Api.Common.Endpoints;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Listing;
using DocQuery.Domain.Documents;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocQuery.Query.Api.Endpoints.Documents;

/// <summary><c>GET /documents/{id}</c>: one document's processing state, polled by the UI after an upload.</summary>
internal sealed class GetDocumentEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/documents/{id:guid}", HandleAsync)
            .WithName("GetDocument")
            .WithTags("Documents")
            .WithSummary("Get a document's processing state.");
    }

    private static async Task<Results<Ok<DocumentResponse>, NotFound>> HandleAsync(
        Guid id,
        IQueryHandler<GetDocumentQuery, DocumentSummary?> handler,
        CancellationToken cancellationToken)
    {
        var summary = await handler.HandleAsync(new GetDocumentQuery(new DocumentId(id)), cancellationToken);

        return summary is null ? TypedResults.NotFound() : TypedResults.Ok(DocumentResponse.From(summary));
    }
}
