using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly.Registry;

namespace DocQuery.Infrastructure;

/// <summary>
/// Composition root for infrastructure adapters (storage, messaging, resilience). Raw adapters are registered
/// under <see cref="ServiceKeys"/> and exposed through resilience decorators.
/// </summary>
public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddBlobStorageResilience(builder.Configuration);
        builder.AddAzureBlobStorage();
        builder.Services.AddResilientBlobStore();

        return builder;
    }

    /// <summary>Exposes <see cref="IBlobStore"/> as the resilience decorator around the keyed raw adapter.</summary>
    public static IServiceCollection AddResilientBlobStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IBlobStore>(provider => new ResilientBlobStore(
            provider.GetRequiredKeyedService<IBlobStore>(ServiceKeys.RawBlobStore),
            provider.GetRequiredService<ResiliencePipelineProvider<string>>()));

        return services;
    }
}
