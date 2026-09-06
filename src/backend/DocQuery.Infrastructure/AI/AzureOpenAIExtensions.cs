using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Identity;
using DocQuery.Domain.Documents;
using DocQuery.Infrastructure.Resilience;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly.Registry;

namespace DocQuery.Infrastructure.AI;

public static class AzureOpenAIExtensions
{
    public const string DefaultConnectionName = "openai";

    /// <summary>
    /// Registers <c>IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt;</c> backed by the deployment in
    /// <c>AzureOpenAI:EmbeddingDeployment</c>, wrapped with OpenTelemetry and the "azure-openai" resilience pipeline.
    /// </summary>
    public static IHostApplicationBuilder AddAzureOpenAIEmbeddings(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAzureOpenAI(connectionName);

        builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            var raw = provider.GetRequiredService<AzureOpenAIClient>()
                .GetEmbeddingClient(options.EmbeddingDeployment)
                .AsIEmbeddingGenerator(DocumentChunk.EmbeddingDimensions);

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(raw)
                .UseOpenTelemetry()
                .Use(inner => new ResilientEmbeddingGenerator(inner, provider.GetRequiredService<ResiliencePipelineProvider<string>>()))
                .Build(provider);
        });

        return builder;
    }

    /// <summary>
    /// Registers <c>IChatClient</c> backed by the deployment in <c>AzureOpenAI:ChatDeployment</c>, wrapped with
    /// OpenTelemetry and the "azure-openai" resilience pipeline.
    /// </summary>
    public static IHostApplicationBuilder AddAzureOpenAIChat(this IHostApplicationBuilder builder, string connectionName = DefaultConnectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAzureOpenAI(connectionName);

        builder.Services.AddSingleton<IChatClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
            var raw = provider.GetRequiredService<AzureOpenAIClient>()
                .GetChatClient(options.ChatDeployment)
                .AsIChatClient();

            return new ChatClientBuilder(raw)
                .UseOpenTelemetry()
                .Use(inner => new ResilientChatClient(inner, provider.GetRequiredService<ResiliencePipelineProvider<string>>()))
                .Build(provider);
        });

        return builder;
    }

    /// <summary>
    /// The shared <see cref="AzureOpenAIClient"/> from <c>ConnectionStrings:{connectionName}</c> (<c>Endpoint=…;Key=…</c>,
    /// or endpoint only for identity-based auth), the validated <see cref="AzureOpenAIOptions"/> and the resilience
    /// pipeline. Idempotent, so embeddings and chat can both be added to one host. The SDK's own retries are disabled
    /// so the Polly pipeline is the single retry authority (ADR 0007).
    /// </summary>
    private static void AddAzureOpenAI(this IHostApplicationBuilder builder, string connectionName)
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(AzureOpenAIClient)))
        {
            return;
        }

        // Fail at startup, with the configuration key named, rather than on the first request.
        var connectionString = builder.Configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException(FormattableString.Invariant(
                $"ConnectionStrings:{connectionName} is missing. Expected 'Endpoint=https://<resource>.openai.azure.com/;Key=<key>' (Key optional with identity-based authentication)."));
        var connection = AzureOpenAIConnectionString.Parse(connectionString);

        builder.Services.AddOptions<AzureOpenAIOptions>()
            .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddAzureOpenAIResilience(builder.Configuration);
        builder.Services.AddSingleton(_ => CreateClient(connection));
    }

    private static AzureOpenAIClient CreateClient(AzureOpenAIConnectionString connection)
    {
        var clientOptions = new AzureOpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };

        return connection.Key is null
            ? new AzureOpenAIClient(connection.Endpoint, new DefaultAzureCredential(), clientOptions)
            : new AzureOpenAIClient(connection.Endpoint, new ApiKeyCredential(connection.Key), clientOptions);
    }
}
