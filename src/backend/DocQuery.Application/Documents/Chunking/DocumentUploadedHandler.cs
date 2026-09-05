using DocQuery.Application.Abstractions;
using DocQuery.Application.Exceptions;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Documents;
using Microsoft.Extensions.Options;

namespace DocQuery.Application.Documents.Chunking;

/// <summary>
/// Consumes <see cref="DocumentUploaded"/>: downloads the PDF, extracts and chunks its text, stores the chunks and
/// marks the document chunked, raising <see cref="DocumentChunkedEvent"/> for the outbox. Idempotent: a redelivery
/// for a document that is already past chunking is ignored. Permanent failures mark the document failed and are
/// rethrown so the consumer dead-letters the message (ADR 0015).
/// </summary>
internal sealed class DocumentUploadedHandler(
    IDocumentRepository documents,
    IBlobStore blobStore,
    IPdfTextExtractor extractor,
    ITokenizer tokenizer,
    IDocumentChunkRepository chunks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<ChunkingOptions> options) : IIntegrationMessageHandler<DocumentUploaded>
{
    public async Task HandleAsync(DocumentUploaded message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var document = await documents.GetByIdAsync(new DocumentId(message.DocumentId), cancellationToken)
            ?? throw new PermanentProcessingException(FormattableString.Invariant($"Document {message.DocumentId} does not exist."));

        if (!document.CanBeChunked)
        {
            return;
        }

        try
        {
            var pages = await ExtractPagesAsync(document, cancellationToken);
            var result = TextChunker.Chunk(pages, tokenizer, options.Value, document.Id);

            if (result.Count == 0)
            {
                throw new PermanentProcessingException("The document contains no extractable text.");
            }

            await chunks.ReplaceForDocumentAsync(document.Id, result, cancellationToken);
            document.MarkChunked(result.Count, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PermanentProcessingException exception)
        {
            // Raised before any chunk was added, so only the status change is saved.
            document.MarkFailed(exception.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<IReadOnlyList<PageText>> ExtractPagesAsync(Document document, CancellationToken cancellationToken)
    {
        Stream content;
        try
        {
            content = await blobStore.DownloadAsync(document.BlobName, cancellationToken);
        }
        catch (BlobNotFoundException exception)
        {
            throw new PermanentProcessingException(exception.Message, exception);
        }

        await using (content)
        {
            return extractor.Extract(content);
        }
    }
}
