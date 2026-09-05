using DocQuery.Application.Abstractions;
using DocQuery.Contracts.Documents;
using DocQuery.Workers.Chunking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocQuery.Workers.Chunking.Tests;

/// <summary>Builds the worker exactly as Program does, in Development so the container is validated, against placeholders.</summary>
public sealed class ChunkingHostTests
{
    // The worker's appsettings.json is copied to the test output, so every value that matters is set explicitly.
    private static readonly Dictionary<string, string?> Placeholders = new()
    {
        ["ConnectionStrings:docquery"] = "Host=localhost;Database=docquery;Username=postgres;Password=placeholder",
        ["ConnectionStrings:documents"] = "Endpoint=https://placeholder.blob.core.windows.net/;ContainerName=documents",
        ["ConnectionStrings:servicebus"] = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=cGxhY2Vob2xkZXI=",
        ["Messaging:TopicName"] = "document-events",
        ["Messaging:Subscription:Name"] = "chunking",
        ["Persistence:ApplyMigrationsOnStartup"] = "false",
        ["BlobStorage:CreateContainerOnStartup"] = "false",
        ["Chunking:MaxTokens"] = "500",
        ["Chunking:OverlapTokens"] = "50",
    };

    [Fact]
    public void Host_composes_with_the_subscription_listener_and_the_chunking_handler()
    {
        using var host = Build([]);

        var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType().Name).ToList();
        Assert.Contains("ServiceBusSubscriptionListener", hostedServices);
        Assert.DoesNotContain("MigrationRunner", hostedServices);
        Assert.DoesNotContain("BlobContainerInitializer", hostedServices);
        Assert.DoesNotContain("OutboxRelayService", hostedServices);

        using var scope = host.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIntegrationMessageHandler<DocumentUploaded>>());
        Assert.NotNull(host.Services.GetRequiredService<ITokenizer>());
        Assert.NotNull(host.Services.GetRequiredService<IPdfTextExtractor>());
    }

    [Fact]
    public async Task Host_fails_to_start_when_overlap_is_not_smaller_than_max_tokens()
    {
        using var host = Build(new Dictionary<string, string?>
        {
            ["Chunking:MaxTokens"] = "100",
            ["Chunking:OverlapTokens"] = "100",
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("OverlapTokens", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_fails_to_start_without_a_subscription_name()
    {
        using var host = Build(new Dictionary<string, string?> { ["Messaging:Subscription:Name"] = string.Empty });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Name", exception.Message, StringComparison.Ordinal);
    }

    private static IHost Build(Dictionary<string, string?> overrides)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(Placeholders);
        builder.Configuration.AddInMemoryCollection(overrides);
        ChunkingHost.Configure(builder);
        return builder.Build();
    }
}
