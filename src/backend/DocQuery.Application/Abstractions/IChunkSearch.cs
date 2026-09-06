using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Documents;

namespace DocQuery.Application.Abstractions;

/// <summary>Nearest-neighbour search over embedded chunks; implemented by persistence with pgvector (ADR 0019).</summary>
public interface IChunkSearch
{
    /// <summary>
    /// The <paramref name="limit"/> chunks closest to <paramref name="queryEmbedding"/> by cosine distance, nearest
    /// first, optionally restricted to one document. Chunks without an embedding are never returned.
    /// </summary>
    Task<IReadOnlyList<ChunkMatch>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, DocumentId? documentId, int limit, CancellationToken cancellationToken);
}
