using System.Diagnostics.CodeAnalysis;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;

namespace DocQuery.Infrastructure.Persistence.Outbox;

internal sealed class DocumentUploadedMapper : IIntegrationEventMapper
{
    public bool TryMap(IDomainEvent domainEvent, [NotNullWhen(true)] out object? message)
    {
        if (domainEvent is DocumentUploadedEvent uploaded)
        {
            message = new DocumentUploaded(uploaded.DocumentId.Value, uploaded.BlobName, uploaded.UploadedAt);
            return true;
        }

        message = null;
        return false;
    }
}
