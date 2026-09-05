using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocQuery.Application.Abstractions;

namespace DocQuery.Infrastructure.Storage;

/// <summary>Raw Azure Blob Storage adapter. Registered under <see cref="ServiceKeys.RawBlobStore"/> and wrapped by <see cref="ResilientBlobStore"/>.</summary>
internal sealed class AzureBlobStore(BlobContainerClient container) : IBlobStore
{
    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        };

        await container.GetBlobClient(blobName).UploadAsync(content, options, cancellationToken);
    }
}
