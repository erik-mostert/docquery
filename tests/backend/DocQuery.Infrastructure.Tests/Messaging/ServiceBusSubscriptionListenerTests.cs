using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Exceptions;
using DocQuery.Application.Messaging;
using DocQuery.Infrastructure.Messaging;
using DocQuery.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Tests.Messaging;

[Collection(ServiceBusCollectionDefinition.Name)]
public sealed class ServiceBusSubscriptionListenerTests(ServiceBusFixture serviceBus)
{
    // Generous: the emulator can be slow to deliver the first message when Docker is busy with other test containers.
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(90);

    [DockerFact]
    public async Task Known_message_is_handled_and_completed()
    {
        var received = new Received();
        await using var client = new ServiceBusClient(serviceBus.ConnectionString);
        await using var listener = CreateListener(client, received);
        await listener.StartAsync(CancellationToken.None);

        var id = await PublishAsync(client, typeof(Ping).FullName!, """{"value":"one"}""");

        await WaitUntilAsync(() => received.Pings.Any(p => p.Value == "one"));
        await listener.StopAsync(CancellationToken.None);

        Assert.DoesNotContain(await PeekAsync(client, SubQueue.None), m => m.MessageId == id);
        Assert.DoesNotContain(await PeekAsync(client, SubQueue.DeadLetter), m => m.MessageId == id);
    }

    [DockerFact]
    public async Task Permanent_failure_is_dead_lettered_with_the_reason()
    {
        await using var client = new ServiceBusClient(serviceBus.ConnectionString);
        await using var listener = CreateListener(client, new Received());
        await listener.StartAsync(CancellationToken.None);

        var id = await PublishAsync(client, typeof(Poison).FullName!, """{"value":"bad"}""");

        var deadLettered = await WaitForDeadLetterAsync(client, id);
        await listener.StopAsync(CancellationToken.None);

        Assert.Equal(nameof(PermanentProcessingException), deadLettered.DeadLetterReason);
        Assert.Contains("cannot be processed", deadLettered.DeadLetterErrorDescription, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task Unknown_subject_is_completed_and_ignored()
    {
        await using var client = new ServiceBusClient(serviceBus.ConnectionString);
        await using var listener = CreateListener(client, new Received());
        await listener.StartAsync(CancellationToken.None);

        var id = await PublishAsync(client, "DocQuery.Contracts.Documents.SomethingElse", """{"x":1}""");

        await WaitUntilAsync(async () => !(await PeekAsync(client, SubQueue.None)).Any(m => m.MessageId == id));
        await listener.StopAsync(CancellationToken.None);

        Assert.DoesNotContain(await PeekAsync(client, SubQueue.DeadLetter), m => m.MessageId == id);
    }

    private static ServiceBusSubscriptionListener CreateListener(ServiceBusClient client, Received received)
    {
        var services = new ServiceCollection();
        services.AddSingleton(received);
        services.AddIntegrationMessageHandler<Ping, PingHandler>();
        services.AddIntegrationMessageHandler<Poison, PoisonHandler>();
        services.AddSingleton<MessageDispatcher>();
        var provider = services.BuildServiceProvider();

        return new ServiceBusSubscriptionListener(
            client,
            provider.GetRequiredService<MessageDispatcher>(),
            Options.Create(new MessagingOptions { TopicName = ServiceBusFixture.Topic }),
            Options.Create(new SubscriptionOptions { Name = ServiceBusFixture.Subscription, MaxConcurrentCalls = 1 }),
            NullLogger<ServiceBusSubscriptionListener>.Instance);
    }

    private static async Task<string> PublishAsync(ServiceBusClient client, string subject, string body)
    {
        await using var sender = client.CreateSender(ServiceBusFixture.Topic);
        var message = new ServiceBusMessage(body) { Subject = subject, ContentType = "application/json", MessageId = Guid.NewGuid().ToString() };
        await sender.SendMessageAsync(message);
        return message.MessageId;
    }

    private static async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekAsync(ServiceBusClient client, SubQueue subQueue)
    {
        await using var receiver = client.CreateReceiver(ServiceBusFixture.Topic, ServiceBusFixture.Subscription, new ServiceBusReceiverOptions { SubQueue = subQueue });
        return await receiver.PeekMessagesAsync(100);
    }

    private static async Task<ServiceBusReceivedMessage> WaitForDeadLetterAsync(ServiceBusClient client, string messageId)
    {
        await using var receiver = client.CreateReceiver(ServiceBusFixture.Topic, ServiceBusFixture.Subscription, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var deadline = DateTimeOffset.UtcNow + Wait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var messages = await receiver.PeekMessagesAsync(100);
            var match = messages.FirstOrDefault(m => m.MessageId == messageId);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Message was not dead-lettered in time.");
    }

    private static Task WaitUntilAsync(Func<bool> condition) => WaitUntilAsync(() => Task.FromResult(condition()));

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Wait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Condition was not met in time.");
    }

    public sealed record Ping(string Value);

    public sealed record Poison(string Value);

    public sealed class Received
    {
        public ConcurrentQueue<Ping> Pings { get; } = new();
    }

    public sealed class PingHandler(Received received) : IIntegrationMessageHandler<Ping>
    {
        public Task HandleAsync(Ping message, CancellationToken cancellationToken)
        {
            received.Pings.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    public sealed class PoisonHandler : IIntegrationMessageHandler<Poison>
    {
        public Task HandleAsync(Poison message, CancellationToken cancellationToken) =>
            throw new PermanentProcessingException("This message cannot be processed.");
    }
}
