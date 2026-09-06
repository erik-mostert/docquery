using DocQuery.Api.Common.Endpoints;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Documents;
using DocQuery.Query.Api.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocQuery.Query.Api.Endpoints.Queries;

/// <summary>
/// <c>POST /queries</c>: answers a question from the embedded documents (retrieval-augmented, ADR 0019).
/// Transport shape is checked here (a body is required); the question's own rules live in the domain and surface
/// as 400 through the domain exception handler. Returns 200 with the answer and its citations.
/// </summary>
internal sealed class AskQuestionEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/queries", HandleAsync)
            .WithName("AskQuestion")
            .WithTags("Queries")
            .WithSummary("Answer a question from the uploaded documents, with citations.")
            .RequireRateLimiting(QueryRateLimitOptions.PolicyName);
    }

    private static async Task<Results<Ok<AskQuestionResponse>, ProblemHttpResult>> HandleAsync(
        AskQuestionRequest? request,
        IQueryHandler<AskQuestionQuery, AskQuestionResult> handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A question is required",
                detail: "Send a JSON body with a 'question' field.");
        }

        var query = new AskQuestionQuery(
            request.Question ?? string.Empty,
            request.DocumentId is { } documentId ? new DocumentId(documentId) : null,
            request.TopK);

        var result = await handler.HandleAsync(query, cancellationToken);

        var citations = result.Citations
            .Select(citation => new CitationResponse(
                citation.Number,
                citation.DocumentId.Value,
                citation.FileName,
                citation.PageNumber,
                citation.Position,
                citation.Excerpt,
                citation.Score))
            .ToList();

        return TypedResults.Ok(new AskQuestionResponse(result.Answer, citations));
    }
}
