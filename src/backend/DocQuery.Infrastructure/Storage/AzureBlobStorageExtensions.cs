using DocQuery.Application.Abstractions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Storage;

public static class AzureBlobStorageExtensions
{
    public const string DefaultConnectionName = "documents";

    /// <summary>
    /// Registers the <c>BlobContainerClient</c> for the documents container through the Aspire integration and the
    /// raw <see cref="AzureBlobStore"/> under <see cref="ServiceKeys.RawBlobStore"/>. The Azure SDK's own retries
    /// are disabled so the Polly pipeline is the single retry authority (ADR 0007).
    /// </summary>
    public static IHostApplicationBuilder AddAzureBlobStorage(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAzureBlobContainerClient(
            connectionName,
            configureClientBuilder: clientBuilder => clientBuilder.ConfigureOptions(options => options.Retry.MaxRetries = 0));

        builder.Services.AddKeyedScoped<IBlobStore, AzureBlobStore>(ServiceKeys.RawBlobStore);

        var options = builder.Configuration.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>() ?? new BlobStorageOptions();
        if (options.CreateContainerOnStartup)
        {
            builder.Services.AddHostedService<BlobContainerInitializer>();
        }

        return builder;
    }
}
