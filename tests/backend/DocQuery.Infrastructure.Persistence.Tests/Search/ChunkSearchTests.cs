using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Persistence.Tests.Search;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class ChunkSearchTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset UploadedAt = PostgresFixture.Now;

    [DockerFact]
    public async Task Returns_the_nearest_chunks_first_with_their_source_and_distance()
    {
        var document = await SeedAsync("nearest.pdf", ("axis zero", 0), ("axis one", 1), ("axis two", 2));

        var matches = await SearchAsync(Query(mostly: 1, alittle: 2), documentId: document.Id, limit: 2);

        Assert.Equal(["axis one", "axis two"], matches.Select(match => match.Text));
        Assert.All(matches, match => Assert.Equal(document.Id, match.DocumentId));
        Assert.All(matches, match => Assert.Equal("nearest.pdf", match.FileName));
        Assert.Equal([2, 3], matches.Select(match => match.PageNumber));
        Assert.Equal([1, 2], matches.Select(match => match.Position));
        Assert.True(matches[0].Distance < matches[1].Distance);
        Assert.InRange(matches[0].Distance, 0, 0.2);
    }

    [DockerFact]
    public async Task Searches_across_documents_unless_scoped_to_one()
    {
        var first = await SeedAsync("first.pdf", ("first zero", 0));
        var second = await SeedAsync("second.pdf", ("second zero", 0), ("second one", 1));

        var all = await SearchAsync(Query(mostly: 0, alittle: 1), documentId: null, limit: 10);
        var scoped = await SearchAsync(Query(mostly: 0, alittle: 1), documentId: second.Id, limit: 10);

        Assert.Contains(all, match => match.DocumentId == first.Id);
        Assert.Contains(all, match => match.DocumentId == second.Id);
        Assert.All(scoped, match => Assert.Equal(second.Id, match.DocumentId));
        Assert.Equal(["second zero", "second one"], scoped.Select(match => match.Text));
    }

    [DockerFact]
    public async Task Ignores_chunks_without_an_embedding()
    {
        var document = await SeedAsync("partial.pdf", ("embedded", 0), ("not embedded", null));

        var matches = await SearchAsync(Query(mostly: 0, alittle: 1), documentId: document.Id, limit: 10);

        var match = Assert.Single(matches);
        Assert.Equal("embedded", match.Text);
    }

    private async Task<IReadOnlyList<Application.Documents.Querying.ChunkMatch>> SearchAsync(float[] query, DocumentId? documentId, int limit)
    {
        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IChunkSearch>().SearchAsync(query, documentId, limit, CancellationToken.None);
    }

    private static float[] Query(int mostly, int alittle)
    {
        var vector = new float[DocumentChunk.EmbeddingDimensions];
        vector[mostly] = 0.9f;
        vector[alittle] = 0.1f;
        return vector;
    }

    /// <summary>Seeds one document whose chunks are unit vectors along the given axis (null: left unembedded).</summary>
    private async Task<Document> SeedAsync(string fileName, params (string Text, int? Axis)[] chunks)
    {
        var document = Document.Upload(fileName, "application/pdf", 10, UploadedAt);
        document.MarkChunked(chunks.Length, UploadedAt);

        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(document, CancellationToken.None);

        var entities = chunks.Select((chunk, index) =>
        {
            var entity = DocumentChunk.Create(document.Id, index, index + 1, chunk.Text, 2);
            if (chunk.Axis is { } axis)
            {
                var vector = new float[DocumentChunk.EmbeddingDimensions];
                vector[axis] = 1f;
                entity.SetEmbedding(vector);
            }

            return entity;
        }).ToList();

        await scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>().ReplaceForDocumentAsync(document.Id, entities, CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return document;
    }
}
