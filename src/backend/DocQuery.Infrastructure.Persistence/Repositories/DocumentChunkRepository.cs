using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocQuery.Infrastructure.Persistence.Repositories;

/// <summary>
/// Replaces chunks through the change tracker (remove existing, add new) rather than a bulk delete, so the swap is
/// committed atomically with the document's status change by the unit of work's single SaveChanges.
/// </summary>
internal sealed class DocumentChunkRepository(DocQueryDbContext context) : IDocumentChunkRepository
{
    public async Task ReplaceForDocumentAsync(DocumentId documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken)
    {
        var existing = await context.DocumentChunks
            .Where(chunk => chunk.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        context.DocumentChunks.RemoveRange(existing);
        await context.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
    }
}
