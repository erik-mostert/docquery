using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Persistence.Tests.Repositories;

[Collection(PostgresCollectionDefinition.Name)]
public sealed class DocumentListingTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Base = new(2026, 9, 6, 8, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task ListAsync_returns_newest_first_up_to_the_limit()
    {
        var prefix = Guid.NewGuid().ToString("N");
        await SeedAsync($"{prefix}-old.pdf", Base);
        await SeedAsync($"{prefix}-new.pdf", Base.AddMinutes(2));
        await SeedAsync($"{prefix}-mid.pdf", Base.AddMinutes(1));

        var listed = await ListAsync(limit: 200);
        var ours = listed.Where(document => document.FileName.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        Assert.Equal([$"{prefix}-new.pdf", $"{prefix}-mid.pdf", $"{prefix}-old.pdf"], ours.Select(document => document.FileName));

        var limited = await ListAsync(limit: 1);
        Assert.Single(limited);
    }

    private async Task<IReadOnlyList<Document>> ListAsync(int limit)
    {
        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().ListAsync(limit, CancellationToken.None);
    }

    private async Task SeedAsync(string fileName, DateTimeOffset uploadedAt)
    {
        await using var services = postgres.CreateServices();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDocumentRepository>().AddAsync(Document.Upload(fileName, "application/pdf", 10, uploadedAt), CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
    }
}
