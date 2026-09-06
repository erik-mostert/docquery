using DocQuery.Application;
using DocQuery.Application.Documents.Embedding;
using DocQuery.Infrastructure.AI;
using DocQuery.Infrastructure.Configuration;
using DocQuery.Infrastructure.Messaging;
using DocQuery.Infrastructure.Persistence;

namespace DocQuery.Workers.Embedding;

/// <summary>Composition of the embedding worker, kept out of Program.cs so tests can build the same host.</summary>
public static class EmbeddingHost
{
    public static HostApplicationBuilder Configure(HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Supplies ConnectionStrings:openai from the vault when a vault URI is configured; otherwise a direct
        // ConnectionStrings:openai value (user secret locally) is used. Host-injected connection strings win.
        builder.AddKeyVaultSecretsIfConfigured();

        builder.AddServiceDefaults();

        builder.Services.AddEmbedding();
        builder.Services.AddOptions<EmbeddingOptions>()
            .Bind(builder.Configuration.GetSection(EmbeddingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddAzureOpenAIEmbeddings();
        builder.AddPersistence();
        builder.AddServiceBusConsumer();

        return builder;
    }
}
