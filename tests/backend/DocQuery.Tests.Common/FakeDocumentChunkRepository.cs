using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;

namespace DocQuery.Tests.Common;

public sealed class FakeDocumentChunkRepository : IDocumentChunkRepository
{
    private readonly Dictionary<DocumentId, IReadOnlyList<DocumentChunk>> _chunks = [];

    public int ReplaceCount { get; private set; }

    public IReadOnlyList<DocumentChunk> ChunksFor(DocumentId documentId) =>
        _chunks.TryGetValue(documentId, out var chunks) ? chunks : [];

    public Task ReplaceForDocumentAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken)
    {
        ReplaceCount++;
        _chunks[documentId] = chunks;
        return Task.CompletedTask;
    }
}
