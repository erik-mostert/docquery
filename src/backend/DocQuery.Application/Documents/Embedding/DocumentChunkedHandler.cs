using DocQuery.Application.Abstractions;
using DocQuery.Application.Exceptions;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Documents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace DocQuery.Application.Documents.Embedding;

/// <summary>
/// Consumes <see cref="DocumentChunked"/>: embeds every chunk that has no embedding yet, in batches, then marks
/// the document embedded and raises <see cref="DocumentEmbeddedEvent"/> for the outbox. Idempotent: a redelivery
/// skips chunks already embedded and documents already past embedding. A wrong number or size of vectors is a
/// configuration error, so it is permanent (ADR 0017).
/// </summary>
internal sealed class DocumentChunkedHandler(
    IDocumentRepository documents,
    IDocumentChunkRepository chunks,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<EmbeddingOptions> options) : IIntegrationMessageHandler<DocumentChunked>
{
    public async Task HandleAsync(DocumentChunked message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var document = await documents.GetByIdAsync(new DocumentId(message.DocumentId), cancellationToken)
            ?? throw new PermanentProcessingException(FormattableString.Invariant($"Document {message.DocumentId} does not exist."));

        if (!document.CanBeEmbedded)
        {
            return;
        }

        try
        {
            var all = await chunks.GetByDocumentAsync(document.Id, cancellationToken);
            if (all.Count == 0)
            {
                throw new PermanentProcessingException("The document has no chunks to embed.");
            }

            var pending = all.Where(chunk => !chunk.HasEmbedding).ToList();
            foreach (var batch in pending.Chunk(options.Value.BatchSize))
            {
                await EmbedBatchAsync(batch, cancellationToken);
            }

            document.MarkEmbedded(timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PermanentProcessingException exception)
        {
            document.MarkFailed(exception.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task EmbedBatchAsync(DocumentChunk[] batch, CancellationToken cancellationToken)
    {
        var generated = await embeddings.GenerateAsync(batch.Select(chunk => chunk.Text), cancellationToken: cancellationToken);

        if (generated.Count != batch.Length)
        {
            throw new PermanentProcessingException(FormattableString.Invariant(
                $"The embedding provider returned {generated.Count} vectors for {batch.Length} inputs."));
        }

        for (var i = 0; i < batch.Length; i++)
        {
            var vector = generated[i].Vector;
            if (vector.Length != DocumentChunk.EmbeddingDimensions)
            {
                throw new PermanentProcessingException(FormattableString.Invariant(
                    $"The embedding provider returned {vector.Length}-dimensional vectors; {DocumentChunk.EmbeddingDimensions} are required. Check the deployment's model."));
            }

            batch[i].SetEmbedding(vector);
        }
    }
}
