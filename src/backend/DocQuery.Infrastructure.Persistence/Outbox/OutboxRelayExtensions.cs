using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocQuery.Infrastructure.Persistence.Outbox;

public static class OutboxRelayExtensions
{
    /// <summary>
    /// Runs the outbox relay in this host. Requires <c>AddPersistence</c> for the DbContext and an
    /// <c>IEventBus</c> registration for the transport.
    /// </summary>
    public static IHostApplicationBuilder AddOutboxRelay(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<OutboxRelayOptions>()
            .Bind(builder.Configuration.GetSection(OutboxRelayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<OutboxProcessor>();
        builder.Services.AddHostedService<OutboxRelayService>();

        return builder;
    }
}
