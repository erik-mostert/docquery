using DocQuery.Application.Abstractions;
using DocQuery.Workers.OutboxRelay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocQuery.Workers.OutboxRelay.Tests;

/// <summary>Builds the worker exactly as Program does, with Development-mode container validation, against placeholders.</summary>
public sealed class OutboxRelayHostTests
{
    [Fact]
    public void Host_composes_with_relay_hosted_service_and_event_bus()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:docquery"] = "Host=localhost;Database=docquery;Username=postgres;Password=placeholder",
            ["ConnectionStrings:servicebus"] = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=cGxhY2Vob2xkZXI=",
            ["Messaging:TopicName"] = "document-events",
            ["Persistence:ApplyMigrationsOnStartup"] = "false",
        });

        OutboxRelayHost.Configure(builder);
        using var host = builder.Build();

        Assert.Contains(host.Services.GetServices<IHostedService>(), service => service.GetType().Name == "OutboxRelayService");
        Assert.DoesNotContain(host.Services.GetServices<IHostedService>(), service => service.GetType().Name == "MigrationRunner");
        Assert.NotNull(host.Services.GetRequiredService<IEventBus>());
    }

    [Fact]
    public async Task Host_fails_to_start_with_an_empty_topic_name()
    {
        // The worker's appsettings.json (copied to the test output) supplies a topic name; override it explicitly.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:docquery"] = "Host=localhost;Database=docquery;Username=postgres;Password=placeholder",
            ["ConnectionStrings:servicebus"] = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=cGxhY2Vob2xkZXI=",
            ["Messaging:TopicName"] = string.Empty,
            ["Persistence:ApplyMigrationsOnStartup"] = "false",
        });
        OutboxRelayHost.Configure(builder);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("TopicName", exception.Message, StringComparison.Ordinal);
    }
}
