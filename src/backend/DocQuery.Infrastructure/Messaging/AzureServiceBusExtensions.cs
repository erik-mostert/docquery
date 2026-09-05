using DocQuery.Application.Abstractions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Messaging;

public static class AzureServiceBusExtensions
{
    public const string DefaultConnectionName = "servicebus";

    /// <summary>
    /// Registers the <c>ServiceBusClient</c> through the Aspire integration and the raw <see cref="ServiceBusEventBus"/>
    /// under <see cref="ServiceKeys.RawEventBus"/>. The SDK's own retries are disabled so the Polly pipeline is the
    /// single retry authority (ADR 0007). <c>Messaging:TopicName</c> is required and validated at startup.
    /// </summary>
    public static IHostApplicationBuilder AddAzureServiceBus(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAzureServiceBusClient(
            connectionName,
            configureClientBuilder: clientBuilder => clientBuilder.ConfigureOptions(options => options.RetryOptions.MaxRetries = 0));

        builder.Services.AddOptions<MessagingOptions>()
            .Bind(builder.Configuration.GetSection(MessagingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddKeyedSingleton<IEventBus, ServiceBusEventBus>(ServiceKeys.RawEventBus);

        return builder;
    }
}
