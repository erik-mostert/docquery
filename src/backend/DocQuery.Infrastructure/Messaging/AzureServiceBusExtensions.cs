using Azure.Messaging.ServiceBus;
using DocQuery.Application.Abstractions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Messaging;

public static class AzureServiceBusExtensions
{
    public const string DefaultConnectionName = "servicebus";

    /// <summary>
    /// Publisher side: the raw <see cref="ServiceBusEventBus"/> under <see cref="ServiceKeys.RawEventBus"/>. The SDK's
    /// own retries are disabled so the Polly pipeline is the single retry authority (ADR 0007).
    /// </summary>
    public static IHostApplicationBuilder AddAzureServiceBus(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceBusClient(connectionName, disableSdkRetries: true);
        builder.Services.AddKeyedSingleton<IEventBus, ServiceBusEventBus>(ServiceKeys.RawEventBus);

        return builder;
    }

    /// <summary>
    /// Consumer side: a <see cref="ServiceBusSubscriptionListener"/> over the subscription named in
    /// <c>Messaging:Subscription</c>, dispatching to handlers registered with <c>AddIntegrationMessageHandler</c>.
    /// The receive and settle path has no Polly wrapper, so the SDK keeps its default retries here.
    /// </summary>
    public static IHostApplicationBuilder AddServiceBusConsumer(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceBusClient(connectionName, disableSdkRetries: false);

        builder.Services.AddOptions<SubscriptionOptions>()
            .Bind(builder.Configuration.GetSection(SubscriptionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddSingleton<MessageDispatcher>();
        builder.Services.AddHostedService<ServiceBusSubscriptionListener>();

        return builder;
    }

    /// <summary>
    /// Registers the shared <see cref="ServiceBusClient"/> (via the Aspire integration: connection string, health
    /// check, tracing) and <see cref="MessagingOptions"/> once per host. A host that both publishes and consumes
    /// keeps the retry setting of whichever role registered first.
    /// </summary>
    private static void AddServiceBusClient(this IHostApplicationBuilder builder, string connectionName, bool disableSdkRetries)
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ServiceBusClient)))
        {
            return;
        }

        builder.AddAzureServiceBusClient(
            connectionName,
            configureClientBuilder: clientBuilder =>
            {
                if (disableSdkRetries)
                {
                    clientBuilder.ConfigureOptions(options => options.RetryOptions.MaxRetries = 0);
                }
            });

        builder.Services.AddOptions<MessagingOptions>()
            .Bind(builder.Configuration.GetSection(MessagingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
