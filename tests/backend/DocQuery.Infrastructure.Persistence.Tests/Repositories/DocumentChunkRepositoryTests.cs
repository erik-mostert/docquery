using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Persistence.Tests.Repositories;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DocumentChunkRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset UploadedAt = PostgresFixture.Now;

    [DockerFact]
    public async Task Replace_stores_chunks_and_a_second_replace_leaves_only_the_new_set()
    {
        var document = await SeedDocumentAsync();
        await using var services = postgres.CreateServices();

        await ReplaceAsync(services, document.Id, Chunks(document.Id, "first", "second", "third"));
        await ReplaceAsync(services, document.Id, Chunks(document.Id, "only-a", "only-b"));

        await using var context = postgres.CreateContext();
        var stored = await context.DocumentChunks.Where(c => c.DocumentId == document.Id).OrderBy(c => c.Position).ToListAsync();
        Assert.Equal(["only-a", "only-b"], stored.Select(c => c.Text).ToArray());
        Assert.Equal([0, 1], stored.Select(c => c.Position).ToArray());
        Assert.All(stored, chunk => Assert.Equal(1, chunk.PageNumber));
    }

    [DockerFact]
    public async Task Deleting_a_document_cascades_to_its_chunks()
    {
        var document = await SeedDocumentAsync();
        await using var services = postgres.CreateServices();
        await ReplaceAsync(services, document.Id, Chunks(document.Id, "a", "b"));

        await using (var context = postgres.CreateContext())
        {
            context.Documents.Remove(await context.Documents.SingleAsync(d => d.Id == document.Id));
            await context.SaveChangesAsync();
        }

        await using var verify = postgres.CreateContext();
        Assert.False(await verify.DocumentChunks.AnyAsync(c => c.DocumentId == document.Id));
    }

    private static async Task ReplaceAsync(ServiceProvider services, DocumentId documentId, IReadOnlyList<DocumentChunk> chunks)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>().ReplaceForDocumentAsync(documentId, chunks, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
    }

    private static List<DocumentChunk> Chunks(DocumentId documentId, params string[] texts) =>
        texts.Select((text, index) => DocumentChunk.Create(documentId, index, pageNumber: 1, text, tokenCount: 1)).ToList();

    private async Task<Document> SeedDocumentAsync()
    {
        var document = Document.Upload("chunks.pdf", "application/pdf", 10, UploadedAt);
        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return document;
    }
}
