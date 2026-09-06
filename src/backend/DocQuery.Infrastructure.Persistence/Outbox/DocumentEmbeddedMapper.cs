using System.Diagnostics.CodeAnalysis;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Infrastructure.Persistence.Outbox;

internal sealed class DocumentEmbeddedMapper : IIntegrationEventMapper
{
    public bool TryMap(IDomainEvent domainEvent, [NotNullWhen(true)] out object? message)
    {
        if (domainEvent is DocumentEmbeddedEvent embedded)
        {
            message = new DocumentEmbedded(embedded.DocumentId.Value, embedded.EmbeddedAt);
            return true;
        }

        message = null;
        return false;
    }
}
