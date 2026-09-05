using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure.Messaging;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly.Registry;

namespace DocQuery.Infrastructure;

/// <summary>
/// Composition root for infrastructure adapters. Raw adapters are registered under <see cref="ServiceKeys"/> and
/// exposed through resilience decorators. Hosts pick the groups they need: the Command API stores blobs, the outbox
/// relay publishes messages.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Blob storage with its resilience pipeline.</summary>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddBlobStorageResilience(builder.Configuration);
        builder.AddAzureBlobStorage();
        builder.Services.AddResilientBlobStore();

        return builder;
    }

    /// <summary>Service Bus publishing with its resilience pipeline.</summary>
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddServiceBusResilience(builder.Configuration);
        builder.AddAzureServiceBus();
        builder.Services.AddResilientEventBus();

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

    /// <summary>Exposes <see cref="IEventBus"/> as the resilience decorator around the keyed raw adapter.</summary>
    public static IServiceCollection AddResilientEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEventBus>(provider => new ResilientEventBus(
            provider.GetRequiredKeyedService<IEventBus>(ServiceKeys.RawEventBus),
            provider.GetRequiredService<ResiliencePipelineProvider<string>>()));

        return services;
    }
}
