using DocQuery.Contracts.Documents;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;
using DocQuery.Infrastructure.Persistence.Outbox;

namespace DocQuery.Infrastructure.Persistence.Tests.Outbox;

public sealed class DocumentUploadedMapperTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_document_uploaded_event_to_contract()
    {
        var id = DocumentId.Create();
        var mapper = new DocumentUploadedMapper();

        var mapped = mapper.TryMap(new DocumentUploadedEvent(id, "blob.pdf", UploadedAt), out var message);

        Assert.True(mapped);
        var contract = Assert.IsType<DocumentUploaded>(message);
        Assert.Equal(id.Value, contract.DocumentId);
        Assert.Equal("blob.pdf", contract.BlobName);
        Assert.Equal(UploadedAt, contract.UploadedAt);
    }

    [Fact]
    public void Ignores_other_events()
    {
        var mapper = new DocumentUploadedMapper();

        var mapped = mapper.TryMap(new OtherEvent(), out var message);

        Assert.False(mapped);
        Assert.Null(message);
    }

    private sealed record OtherEvent : IDomainEvent;
}
