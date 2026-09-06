using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace DocQuery.Infrastructure.Persistence.Tests.Repositories;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DocumentChunkEmbeddingTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset UploadedAt = PostgresFixture.Now;

    [DockerFact]
    public async Task Embeddings_round_trip_through_the_vector_column()
    {
        var document = await SeedDocumentWithChunksAsync("alpha", "beta");
        await using var services = postgres.CreateServices();
        await using (var scope = services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>();
            var chunks = await repository.GetByDocumentAsync(document.Id, CancellationToken.None);
            chunks[0].SetEmbedding(UnitVector(axis: 0));
            chunks[1].SetEmbedding(UnitVector(axis: 1));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        }

        await using var context = postgres.CreateContext();
        var stored = await context.DocumentChunks.Where(c => c.DocumentId == document.Id).OrderBy(c => c.Position).ToListAsync();
        Assert.All(stored, chunk => Assert.True(chunk.HasEmbedding));
        Assert.Equal(DocumentChunk.EmbeddingDimensions, stored[0].Embedding!.Value.Length);
        Assert.Equal(1f, stored[0].Embedding!.Value.Span[0]);
        Assert.Equal(1f, stored[1].Embedding!.Value.Span[1]);
    }

    [DockerFact]
    public async Task GetByDocumentAsync_returns_chunks_in_position_order()
    {
        var document = await SeedDocumentWithChunksAsync("first", "second", "third");
        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();

        var chunks = await scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>().GetByDocumentAsync(document.Id, CancellationToken.None);

        Assert.Equal(["first", "second", "third"], chunks.Select(c => c.Text).ToArray());
    }

    [DockerFact]
    public async Task Nearest_chunk_by_cosine_distance_can_be_queried_with_raw_sql()
    {
        var document = await SeedDocumentWithChunksAsync("axis zero", "axis one", "axis two");
        await using (var context = postgres.CreateContext())
        {
            var chunks = await context.DocumentChunks.Where(c => c.DocumentId == document.Id).OrderBy(c => c.Position).ToListAsync();
            for (var i = 0; i < chunks.Count; i++)
            {
                chunks[i].SetEmbedding(UnitVector(axis: i));
            }

            await context.SaveChangesAsync();
        }

        // Mostly along axis 1 with a little of axis 2: cosine-nearest is "axis one".
        var query = new float[DocumentChunk.EmbeddingDimensions];
        query[1] = 0.9f;
        query[2] = 0.1f;

        await using var verify = postgres.CreateContext();
        var nearest = await verify.DocumentChunks
            .FromSql($"""
                SELECT * FROM document_chunks
                WHERE "DocumentId" = {document.Id.Value} AND "Embedding" IS NOT NULL
                ORDER BY "Embedding" <=> {new Vector(query)}
                LIMIT 1
                """)
            .SingleAsync();

        Assert.Equal("axis one", nearest.Text);
    }

    private static float[] UnitVector(int axis)
    {
        var vector = new float[DocumentChunk.EmbeddingDimensions];
        vector[axis] = 1f;
        return vector;
    }

    private async Task<Document> SeedDocumentWithChunksAsync(params string[] texts)
    {
        var document = Document.Upload("embed.pdf", "application/pdf", 10, UploadedAt);
        document.MarkChunked(texts.Length, UploadedAt);
        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);
        var chunks = texts.Select((text, index) => DocumentChunk.Create(document.Id, index, 1, text, 2)).ToList();
        await scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>().ReplaceForDocumentAsync(document.Id, chunks, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return document;
    }
}
