using DocQuery.Infrastructure;
using DocQuery.Infrastructure.Persistence;
using DocQuery.Infrastructure.Persistence.Outbox;

namespace DocQuery.Workers.OutboxRelay;

/// <summary>Composition of the relay worker, kept out of Program.cs so tests can build the same host.</summary>
public static class OutboxRelayHost
{
    public static HostApplicationBuilder Configure(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddServiceDefaults();
        builder.AddPersistence();
        builder.AddMessaging();
        builder.AddOutboxRelay();

        return builder;
    }
}
