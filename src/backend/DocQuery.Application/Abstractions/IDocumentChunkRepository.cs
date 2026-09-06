using DocQuery.Domain.Documents;

namespace DocQuery.Application.Abstractions;

public interface IDocumentChunkRepository
{
    /// <summary>
    /// Replaces every chunk of the document with <paramref name="chunks"/>. Committed by the unit of work together
    /// with the document's status change, so a redelivered message can safely re-chunk.
    /// </summary>
    Task ReplaceForDocumentAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken);

    /// <summary>The document's chunks in position order, tracked so changes to them are saved by the unit of work.</summary>
    Task<IReadOnlyList<DocumentChunk>> GetByDocumentAsync(DocumentId documentId, CancellationToken cancellationToken);
}
