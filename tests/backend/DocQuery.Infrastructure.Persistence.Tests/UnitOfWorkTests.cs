using System.Text.Json;
using DocQuery.Application.Abstractions;
using DocQuery.Contracts.Documents;
using DocQuery.Domain.Documents;
using DocQuery.Infrastructure.Persistence.Outbox;
using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocQuery.Infrastructure.Persistence.Tests;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class UnitOfWorkTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset UploadedAt = PostgresFixture.Now;

    [DockerFact]
    public async Task Saving_a_new_document_writes_a_document_uploaded_outbox_message()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);
        await using var services = postgres.CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        Assert.Empty(document.DomainEvents);

        await using var context = postgres.CreateContext();
        // Payload is jsonb, which has no LIKE translation; filter by type in SQL and by id in memory.
        var candidates = await context.OutboxMessages.Where(m => m.Type == typeof(DocumentUploaded).FullName).ToListAsync();
        var message = Assert.Single(candidates, m => m.Payload.Contains(document.Id.Value.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Null(message.ProcessedAt);
        Assert.Equal(UploadedAt, message.OccurredAt);

        var contract = JsonSerializer.Deserialize<DocumentUploaded>(message.Payload, OutboxMessage.SerializerOptions);
        Assert.NotNull(contract);
        Assert.Equal(document.Id.Value, contract.DocumentId);
        Assert.Equal(document.BlobName, contract.BlobName);
    }

    [DockerFact]
    public async Task Marking_a_document_chunked_writes_a_document_chunked_outbox_message()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);
        document.ClearDomainEvents();
        await using var services = postgres.CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var tracked = await repository.GetByIdAsync(document.Id, CancellationToken.None);
            tracked!.MarkChunked(4, UploadedAt.AddMinutes(1));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using var context = postgres.CreateContext();
        var candidates = await context.OutboxMessages.Where(m => m.Type == typeof(DocumentChunked).FullName).ToListAsync();
        var message = Assert.Single(candidates, m => m.Payload.Contains(document.Id.Value.ToString(), StringComparison.OrdinalIgnoreCase));
        var contract = JsonSerializer.Deserialize<DocumentChunked>(message.Payload, OutboxMessage.SerializerOptions);
        Assert.NotNull(contract);
        Assert.Equal(4, contract.ChunkCount);
        Assert.Equal(UploadedAt.AddMinutes(1), contract.ChunkedAt);
    }

    [DockerFact]
    public async Task Marking_a_document_embedded_writes_a_document_embedded_outbox_message()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);
        document.MarkChunked(2, UploadedAt);
        document.ClearDomainEvents();
        await using var services = postgres.CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var tracked = await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().GetByIdAsync(document.Id, CancellationToken.None);
            tracked!.MarkEmbedded(UploadedAt.AddMinutes(2));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using var context = postgres.CreateContext();
        var candidates = await context.OutboxMessages.Where(m => m.Type == typeof(DocumentEmbedded).FullName).ToListAsync();
        var message = Assert.Single(candidates, m => m.Payload.Contains(document.Id.Value.ToString(), StringComparison.OrdinalIgnoreCase));
        var contract = JsonSerializer.Deserialize<DocumentEmbedded>(message.Payload, OutboxMessage.SerializerOptions);
        Assert.NotNull(contract);
        Assert.Equal(UploadedAt.AddMinutes(2), contract.EmbeddedAt);
    }

    [DockerFact]
    public async Task Saving_fails_when_a_domain_event_has_no_mapper()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);
        await using var services = postgres.CreateServices(s => s.RemoveAll<IIntegrationEventMapper>());
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));

        await using var context = postgres.CreateContext();
        Assert.False(await context.Documents.AnyAsync(d => d.Id == document.Id));
    }
}
