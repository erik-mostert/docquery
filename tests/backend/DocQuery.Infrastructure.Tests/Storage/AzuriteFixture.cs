using Azure.Storage.Blobs;
using DocQuery.Tests.Common;
using Testcontainers.Azurite;

namespace DocQuery.Infrastructure.Tests.Storage;

/// <summary>One Azurite container shared by the blob storage tests.</summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();

    public string ConnectionString => _container.GetConnectionString();

    public BlobContainerClient Container(string name) => new(ConnectionString, name);

    public async Task InitializeAsync()
    {
        if (DockerAvailability.IsAvailable)
        {
            await _container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AzuriteCollectionDefinition : ICollectionFixture<AzuriteFixture>
{
    public const string Name = "azurite";
}
