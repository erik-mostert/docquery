using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Querying;
using DocQuery.Domain.Documents;

namespace DocQuery.Tests.Common;

/// <summary>Returns the seeded matches (nearest first, as seeded) and records every call.</summary>
public sealed class FakeChunkSearch : IChunkSearch
{
    public List<ChunkMatch> Matches { get; } = [];

    public List<(ReadOnlyMemory<float> Embedding, DocumentId? DocumentId, int Limit)> Calls { get; } = [];

    public Task<IReadOnlyList<ChunkMatch>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, DocumentId? documentId, int limit, CancellationToken cancellationToken)
    {
        Calls.Add((queryEmbedding, documentId, limit));

        IReadOnlyList<ChunkMatch> result = Matches
            .Where(match => documentId is null || match.DocumentId == documentId)
            .Take(limit)
            .ToList();

        return Task.FromResult(result);
    }
}
