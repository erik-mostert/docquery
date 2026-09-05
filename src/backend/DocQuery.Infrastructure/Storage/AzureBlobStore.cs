using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Exceptions;

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

    public async Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var result = await container.GetBlobClient(blobName).DownloadContentAsync(cancellationToken);
            return result.Value.Content.ToStream();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new BlobNotFoundException(blobName, exception);
        }
    }
}
