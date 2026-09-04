using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.Registry;

namespace DocQuery.Infrastructure;

/// <summary>
/// Composition root for infrastructure adapters (storage, messaging, resilience). Raw adapters are registered
/// under <see cref="ServiceKeys"/> and exposed through resilience decorators.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddBlobStorageResilience(configuration);

        services.AddScoped<IBlobStore>(provider => new ResilientBlobStore(
            provider.GetRequiredKeyedService<IBlobStore>(ServiceKeys.RawBlobStore),
            provider.GetRequiredService<ResiliencePipelineProvider<string>>()));

        return services;
    }
}
