using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Storage;

/// <summary>Creates the documents container on startup so local runs against Azurite need no manual setup.</summary>
internal sealed class BlobContainerInitializer(BlobContainerClient container) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
