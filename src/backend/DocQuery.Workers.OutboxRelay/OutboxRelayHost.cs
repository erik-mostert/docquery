using DocQuery.Infrastructure;
using DocQuery.Infrastructure.Configuration;
using DocQuery.Infrastructure.Persistence;
using DocQuery.Infrastructure.Persistence.Outbox;

namespace DocQuery.Workers.OutboxRelay;

/// <summary>Composition of the relay worker, kept out of Program.cs so tests can build the same host.</summary>
public static class OutboxRelayHost
{
    public static HostApplicationBuilder Configure(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // In Kubernetes the PostgreSQL connection string comes from Key Vault through workload identity (ADR 0021);
        // locally the AppHost injects it and no vault URI is configured.
        builder.AddKeyVaultSecretsIfConfigured();

        builder.AddServiceDefaults();
        builder.AddPersistence();
        builder.AddMessaging();
        builder.AddOutboxRelay();

        return builder;
    }
}
