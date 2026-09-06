using System.Net;
using System.Net.Http.Json;
using DocQuery.Domain.Documents;
using DocQuery.Query.Api.Endpoints.Documents;

namespace DocQuery.Query.Api.Tests.Documents;

public sealed class DocumentEndpointsTests(QueryApiFactory factory) : IClassFixture<QueryApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_documents_lists_newest_first_with_status_as_text()
    {
        var older = await SeedAsync("older.pdf", Now);
        var newer = await SeedAsync("newer.pdf", Now.AddMinutes(1));
        newer.MarkChunked(3, Now.AddMinutes(2));
        newer.MarkEmbedded(Now.AddMinutes(3));
        using var client = factory.CreateClient();

        var documents = await client.GetFromJsonAsync<List<DocumentResponse>>(new Uri("/documents?limit=200", UriKind.Relative));

        Assert.NotNull(documents);
        var ours = documents.Where(document => document.Id == older.Id.Value || document.Id == newer.Id.Value).ToList();
        Assert.Equal([newer.Id.Value, older.Id.Value], ours.Select(document => document.Id));
        Assert.Equal("Embedded", ours[0].Status);
        Assert.Equal(3, ours[0].ChunkCount);
        Assert.Equal(Now.AddMinutes(3), ours[0].EmbeddedAt);
        Assert.Equal("Uploaded", ours[1].Status);
    }

    [Fact]
    public async Task Get_documents_with_a_bad_limit_is_a_bad_request()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/documents?limit=0", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_document_by_id_returns_its_state()
    {
        var document = await SeedAsync("one.pdf", Now);
        document.MarkFailed("No text found.");
        using var client = factory.CreateClient();

        var response = await client.GetFromJsonAsync<DocumentResponse>(new Uri($"/documents/{document.Id.Value}", UriKind.Relative));

        Assert.NotNull(response);
        Assert.Equal("one.pdf", response.FileName);
        Assert.Equal("Failed", response.Status);
        Assert.Equal("No text found.", response.FailureReason);
    }

    [Fact]
    public async Task Get_unknown_document_is_not_found()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/documents/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Document> SeedAsync(string fileName, DateTimeOffset uploadedAt)
    {
        var document = Document.Upload(fileName, "application/pdf", 10, uploadedAt);
        await factory.Documents.AddAsync(document, CancellationToken.None);
        return document;
    }
}
