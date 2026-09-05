using DocQuery.Tests.Common;
using Testcontainers.ServiceBus;

namespace DocQuery.Infrastructure.Tests.Messaging;

/// <summary>
/// One Service Bus emulator (plus its SQL dependency, started by the module) shared by the messaging tests. The
/// module's default configuration defines topic "topic.1" with three subscriptions; "subscription.1" and
/// "subscription.2" carry correlation/SQL filters, "subscription.3" has none and receives everything.
/// </summary>
public sealed class ServiceBusFixture : IAsyncLifetime
{
    public const string Topic = "topic.1";
    public const string Subscription = "subscription.3";

    private readonly ServiceBusContainer _container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
        .WithAcceptLicenseAgreement(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (DockerAvailability.IsAvailable)
        {
            await _container.StartAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ServiceBusCollectionDefinition : ICollectionFixture<ServiceBusFixture>
{
    public const string Name = "servicebus";
}
