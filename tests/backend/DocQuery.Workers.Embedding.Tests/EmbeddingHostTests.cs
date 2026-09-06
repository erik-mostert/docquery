using DocQuery.Application.Abstractions;
using DocQuery.Contracts.Documents;
using DocQuery.Workers.Embedding;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocQuery.Workers.Embedding.Tests;

/// <summary>Builds the worker exactly as Program does, in Development so the container is validated, against placeholders.</summary>
public sealed class EmbeddingHostTests
{
    // The worker's appsettings.json is copied to the test output, so every value that matters is set explicitly.
    private static readonly Dictionary<string, string?> Placeholders = new()
    {
        ["ConnectionStrings:docquery"] = "Host=localhost;Database=docquery;Username=postgres;Password=placeholder",
        ["ConnectionStrings:servicebus"] = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=cGxhY2Vob2xkZXI=",
        ["ConnectionStrings:openai"] = "Endpoint=https://placeholder.openai.azure.com/;Key=placeholder",
        ["Messaging:TopicName"] = "document-events",
        ["Messaging:Subscription:Name"] = "embedding",
        ["Persistence:ApplyMigrationsOnStartup"] = "false",
        ["Embedding:BatchSize"] = "16",
        ["AzureOpenAI:EmbeddingDeployment"] = "text-embedding-3-small",
    };

    [Fact]
    public void Host_composes_with_the_subscription_listener_the_handler_and_the_generator()
    {
        using var host = Build([]);

        var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType().Name).ToList();
        Assert.Contains("ServiceBusSubscriptionListener", hostedServices);
        Assert.DoesNotContain("MigrationRunner", hostedServices);

        using var scope = host.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIntegrationMessageHandler<DocumentChunked>>());
        Assert.NotNull(host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Fact]
    public void Configure_fails_immediately_without_an_openai_connection_string()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(Placeholders);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:openai"] = null });

        var exception = Assert.Throws<InvalidOperationException>(() => EmbeddingHost.Configure(builder));

        Assert.Contains("ConnectionStrings:openai", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_fails_to_start_with_an_invalid_batch_size()
    {
        using var host = Build(new Dictionary<string, string?> { ["Embedding:BatchSize"] = "0" });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("BatchSize", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_fails_to_start_without_an_embedding_deployment_name()
    {
        using var host = Build(new Dictionary<string, string?> { ["AzureOpenAI:EmbeddingDeployment"] = string.Empty });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("EmbeddingDeployment", exception.Message, StringComparison.Ordinal);
    }

    private static IHost Build(Dictionary<string, string?> overrides)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(Placeholders);
        builder.Configuration.AddInMemoryCollection(overrides);
        EmbeddingHost.Configure(builder);
        return builder.Build();
    }
}
