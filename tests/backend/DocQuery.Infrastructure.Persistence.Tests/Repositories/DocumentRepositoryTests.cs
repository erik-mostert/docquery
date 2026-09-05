using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Persistence.Tests.Repositories;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DocumentRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Added_document_round_trips_through_the_database()
    {
        var document = Document.Upload("report.pdf", "application/pdf", 1234, UploadedAt);
        await using var services = postgres.CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using var context = postgres.CreateContext();
        var stored = await context.Documents.SingleAsync(d => d.Id == document.Id);

        Assert.Equal("report.pdf", stored.FileName);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.Equal(1234, stored.SizeInBytes);
        Assert.Equal(document.BlobName, stored.BlobName);
        Assert.Equal(DocumentStatus.Uploaded, stored.Status);
        Assert.Equal(UploadedAt, stored.UploadedAt);
        Assert.Empty(stored.DomainEvents);
    }
}
