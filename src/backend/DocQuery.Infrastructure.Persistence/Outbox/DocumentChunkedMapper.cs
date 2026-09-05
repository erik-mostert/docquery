using System.Diagnostics.CodeAnalysis;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Infrastructure.Persistence.Outbox;

internal sealed class DocumentChunkedMapper : IIntegrationEventMapper
{
    public bool TryMap(IDomainEvent domainEvent, [NotNullWhen(true)] out object? message)
    {
        if (domainEvent is DocumentChunkedEvent chunked)
        {
            message = new DocumentChunked(chunked.DocumentId.Value, chunked.ChunkCount, chunked.ChunkedAt);
            return true;
        }

        message = null;
        return false;
    }
}
