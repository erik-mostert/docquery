using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;

namespace DocQuery.Tests.Common;

public sealed class FakeDocumentChunkRepository : IDocumentChunkRepository
{
    private readonly Dictionary<DocumentId, List<DocumentChunk>> _chunks = [];

    public int ReplaceCount { get; private set; }

    public IReadOnlyList<DocumentChunk> ChunksFor(DocumentId documentId) =>
        _chunks.TryGetValue(documentId, out var chunks) ? chunks : [];

    /// <summary>Stores chunks directly, as if a previous chunking run had saved them.</summary>
    public void Seed(DocumentId documentId, IEnumerable<DocumentChunk> chunks) => _chunks[documentId] = chunks.ToList();

    public Task ReplaceForDocumentAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken)
    {
        ReplaceCount++;
        _chunks[documentId] = chunks.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentChunk>> GetByDocumentAsync(DocumentId documentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentChunk>>(ChunksFor(documentId).OrderBy(chunk => chunk.Position).ToList());
}
