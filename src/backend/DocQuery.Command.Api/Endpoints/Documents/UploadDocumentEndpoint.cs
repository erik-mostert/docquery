using DocQuery.Api.Common.Endpoints;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Upload;
using DocQuery.Command.Api.Options;
using DocQuery.Command.Api.RateLimiting;
using DocQuery.Domain.Documents;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocQuery.Command.Api.Endpoints.Documents;

/// <summary>
/// <c>POST /documents</c>: multipart upload of one PDF. Validates transport shape here (file present, non-empty,
/// within the size cap); domain invariants such as the PDF content type are enforced by <see cref="Document"/>
/// and surface as 400 through the domain exception handler. Returns 202 because processing continues
/// asynchronously (ADR 0011).
/// </summary>
internal sealed class UploadDocumentEndpoint(IOptions<UploadOptions> uploadOptions) : IEndpoint
{
    /// <summary>Headroom for multipart boundaries and headers on top of the file itself.</summary>
    private const long MultipartOverheadBytes = 1024 * 1024;

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/documents", HandleAsync)
            .WithName("UploadDocument")
            .WithTags("Documents")
            .WithSummary("Upload a PDF document for processing.")
            .DisableAntiforgery()
            .RequireRateLimiting(UploadRateLimitOptions.PolicyName)
            .WithMetadata(new RequestSizeLimitAttribute(uploadOptions.Value.MaxFileSizeBytes + MultipartOverheadBytes));
    }

    private static async Task<Results<Accepted<UploadDocumentResponse>, ProblemHttpResult>> HandleAsync(
        IFormFile? file,
        ICommandHandler<UploadDocumentCommand, DocumentId> handler,
        IOptions<UploadOptions> options,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A file is required",
                detail: "Send a non-empty PDF in the multipart 'file' field.");
        }

        var maxBytes = options.Value.MaxFileSizeBytes;
        if (file.Length > maxBytes)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "File too large",
                detail: FormattableString.Invariant($"The file is {file.Length} bytes; the limit is {maxBytes} bytes."));
        }

        await using var content = file.OpenReadStream();
        var command = new UploadDocumentCommand(file.FileName, file.ContentType, file.Length, content);

        var documentId = await handler.HandleAsync(command, cancellationToken);

        return TypedResults.Accepted((string?)null, new UploadDocumentResponse(documentId.Value));
    }
}
