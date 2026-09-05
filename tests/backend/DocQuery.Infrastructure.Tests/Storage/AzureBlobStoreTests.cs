using DocQuery.Application.Exceptions;
using DocQuery.Infrastructure.Storage;
using DocQuery.Tests.Common;

namespace DocQuery.Infrastructure.Tests.Storage;

[Collection(AzuriteCollectionDefinition.Name)]
public sealed class AzureBlobStoreTests(AzuriteFixture azurite)
{
    private static readonly byte[] First = "%PDF-1.4 first"u8.ToArray();
    private static readonly byte[] Second = "%PDF-1.4 second, longer"u8.ToArray();

    [DockerFact]
    public async Task Upload_stores_bytes_and_content_type()
    {
        var container = azurite.Container("documents-upload");
        await container.CreateIfNotExistsAsync();
        var store = new AzureBlobStore(container);
        using var content = new MemoryStream(First);

        await store.UploadAsync("doc.pdf", content, "application/pdf", CancellationToken.None);

        var blob = container.GetBlobClient("doc.pdf");
        var downloaded = await blob.DownloadContentAsync();
        Assert.Equal(First, downloaded.Value.Content.ToArray());
        Assert.Equal("application/pdf", downloaded.Value.Details.ContentType);
    }

    [DockerFact]
    public async Task Upload_overwrites_an_existing_blob()
    {
        var container = azurite.Container("documents-overwrite");
        await container.CreateIfNotExistsAsync();
        var store = new AzureBlobStore(container);
        using var first = new MemoryStream(First);
        using var second = new MemoryStream(Second);

        await store.UploadAsync("doc.pdf", first, "application/pdf", CancellationToken.None);
        await store.UploadAsync("doc.pdf", second, "application/pdf", CancellationToken.None);

        var downloaded = await container.GetBlobClient("doc.pdf").DownloadContentAsync();
        Assert.Equal(Second, downloaded.Value.Content.ToArray());
    }

    [DockerFact]
    public async Task Download_returns_the_uploaded_bytes_as_a_seekable_stream()
    {
        var container = azurite.Container("documents-download");
        await container.CreateIfNotExistsAsync();
        var store = new AzureBlobStore(container);
        using var content = new MemoryStream(First);
        await store.UploadAsync("doc.pdf", content, "application/pdf", CancellationToken.None);

        await using var downloaded = await store.DownloadAsync("doc.pdf", CancellationToken.None);

        Assert.True(downloaded.CanSeek);
        using var buffer = new MemoryStream();
        await downloaded.CopyToAsync(buffer);
        Assert.Equal(First, buffer.ToArray());
    }

    [DockerFact]
    public async Task Download_of_a_missing_blob_throws_BlobNotFound()
    {
        var container = azurite.Container("documents-missing");
        await container.CreateIfNotExistsAsync();
        var store = new AzureBlobStore(container);

        var exception = await Assert.ThrowsAsync<BlobNotFoundException>(() => store.DownloadAsync("nope.pdf", CancellationToken.None));

        Assert.Equal("nope.pdf", exception.BlobName);
    }

    [DockerFact]
    public async Task Container_initializer_creates_the_container_when_missing()
    {
        var container = azurite.Container("documents-init");
        var initializer = new BlobContainerInitializer(container);

        await initializer.StartAsync(CancellationToken.None);

        Assert.True(await container.ExistsAsync());
    }
}
