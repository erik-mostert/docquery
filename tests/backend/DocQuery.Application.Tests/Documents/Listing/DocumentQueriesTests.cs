using DocQuery.Application.Abstractions;
using DocQuery.Application.Documents.Listing;
using DocQuery.Domain.Common;
using DocQuery.Domain.Documents;
using DocQuery.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Application.Tests.Documents.Listing;

public sealed class DocumentQueriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryDocumentRepository _documents = new();

    [Fact]
    public async Task List_returns_summaries_newest_first_with_the_default_limit()
    {
        await SeedAsync("old.pdf", Now);
        var newest = await SeedAsync("new.pdf", Now.AddMinutes(1));
        newest.MarkChunked(4, Now.AddMinutes(2));
        var handler = Resolve<IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>>>();

        var result = await handler.HandleAsync(new ListDocumentsQuery(), CancellationToken.None);

        Assert.Equal(["new.pdf", "old.pdf"], result.Select(summary => summary.FileName));
        Assert.Equal(DocumentStatus.Chunked, result[0].Status);
        Assert.Equal(4, result[0].ChunkCount);
        Assert.Equal(Now.AddMinutes(2), result[0].ChunkedAt);
        Assert.Null(result[0].EmbeddedAt);
    }

    [Fact]
    public async Task List_honours_an_explicit_limit()
    {
        await SeedAsync("a.pdf", Now);
        await SeedAsync("b.pdf", Now.AddMinutes(1));
        var handler = Resolve<IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>>>();

        var result = await handler.HandleAsync(new ListDocumentsQuery(Limit: 1), CancellationToken.None);

        Assert.Equal("b.pdf", Assert.Single(result).FileName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task List_rejects_a_limit_outside_the_allowed_range(int limit)
    {
        var handler = Resolve<IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentSummary>>>();

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(new ListDocumentsQuery(limit), CancellationToken.None));
    }

    [Fact]
    public async Task Get_returns_the_summary_including_failure_reason()
    {
        var document = await SeedAsync("bad.pdf", Now);
        document.MarkFailed("No text found.");
        var handler = Resolve<IQueryHandler<GetDocumentQuery, DocumentSummary?>>();

        var result = await handler.HandleAsync(new GetDocumentQuery(document.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(document.Id, result.Id);
        Assert.Equal(DocumentStatus.Failed, result.Status);
        Assert.Equal("No text found.", result.FailureReason);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_document()
    {
        var handler = Resolve<IQueryHandler<GetDocumentQuery, DocumentSummary?>>();

        var result = await handler.HandleAsync(new GetDocumentQuery(DocumentId.Create()), CancellationToken.None);

        Assert.Null(result);
    }

    private async Task<Document> SeedAsync(string fileName, DateTimeOffset uploadedAt)
    {
        var document = Document.Upload(fileName, "application/pdf", 10, uploadedAt);
        await _documents.AddAsync(document, CancellationToken.None);
        return document;
    }

    private THandler Resolve<THandler>()
        where THandler : notnull
    {
        var services = new ServiceCollection();
        services.AddQuerying();
        services.AddSingleton<IDocumentRepository>(_documents);
        return services.BuildServiceProvider().GetRequiredService<THandler>();
    }
}
