using System.Net;
using System.Net.Http.Json;
using DocQuery.Application.Abstractions;
using DocQuery.Command.Api.Endpoints.Documents;
using DocQuery.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly.CircuitBreaker;

namespace DocQuery.Command.Api.Tests.Documents;

public sealed class UploadDocumentEndpointTests(CommandApiFactory factory) : IClassFixture<CommandApiFactory>
{
    [Fact]
    public async Task Post_pdf_returns_accepted_with_document_id_and_stores_blob_and_document()
    {
        using var client = factory.CreateClient();
        using var form = TestContent.PdfForm();

        using var response = await client.PostAsync(TestContent.DocumentsUri, form);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UploadDocumentResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.DocumentId);

        var blobName = FormattableString.Invariant($"{body.DocumentId}.pdf");
        Assert.Contains(factory.BlobStore.Uploads, upload =>
            upload.BlobName == blobName && upload.ContentType == "application/pdf" && upload.Length == TestContent.PdfBytes.Length);
        Assert.Contains(factory.Documents.Documents, document =>
            document.Id.Value == body.DocumentId && document.FileName == "report.pdf" && document.UploadedAt == CommandApiFactory.Now);
    }

    [Fact]
    public async Task Post_without_file_part_returns_bad_request_problem()
    {
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent { { new StringContent("value"), "other" } };

        using var response = await client.PostAsync(TestContent.DocumentsUri, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_empty_file_returns_bad_request_problem()
    {
        using var client = factory.CreateClient();
        using var form = TestContent.FileForm([], "application/pdf");

        using var response = await client.PostAsync(TestContent.DocumentsUri, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_non_pdf_returns_bad_request_problem_and_stores_nothing()
    {
        using var client = factory.CreateClient();
        using var form = TestContent.FileForm("hello"u8.ToArray(), "text/plain", "notes.txt");
        var callsBefore = factory.BlobStore.CallCount;

        using var response = await client.PostAsync(TestContent.DocumentsUri, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(callsBefore, factory.BlobStore.CallCount);
    }

    [Fact]
    public async Task Post_file_over_configured_limit_returns_payload_too_large()
    {
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Upload:MaxFileSizeBytes"] = "1024",
            }))).CreateClient();
        using var form = TestContent.FileForm(new byte[1025], "application/pdf");

        using var response = await client.PostAsync(TestContent.DocumentsUri, form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_when_storage_circuit_is_open_returns_service_unavailable_with_retry_after()
    {
        var brokenStore = new FakeBlobStore();
        brokenStore.FailNextCallWith(new BrokenCircuitException("blob storage circuit is open"));
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBlobStore>();
                services.AddSingleton<IBlobStore>(brokenStore);
            })).CreateClient();
        using var form = TestContent.PdfForm();

        using var response = await client.PostAsync(TestContent.DocumentsUri, form);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("Retry-After"));
    }
}
