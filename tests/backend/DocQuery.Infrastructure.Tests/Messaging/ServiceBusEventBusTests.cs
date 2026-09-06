using System.Globalization;
using Azure.Messaging.ServiceBus;
using DocQuery.Application.Messaging;
using DocQuery.Infrastructure.Messaging;
using DocQuery.Tests.Common;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Tests.Messaging;

[Collection(ServiceBusCollectionDefinition.Name)]
public sealed class ServiceBusEventBusTests(ServiceBusFixture serviceBus)
{
    [DockerFact]
    public async Task Publish_delivers_message_with_id_subject_content_type_and_json_body()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero);
        var message = new OutboundMessage(Guid.NewGuid(), "DocQuery.Contracts.Documents.DocumentUploaded", """{"documentId":"abc"}""", occurredAt);
        await using var client = new ServiceBusClient(serviceBus.ConnectionString);
        await using var bus = new ServiceBusEventBus(client, Options.Create(new MessagingOptions { TopicName = ServiceBusFixture.Topic }));

        await bus.PublishAsync(message, CancellationToken.None);

        var received = await ReceiveByIdAsync(client, message.MessageId.ToString());

        Assert.Equal(message.MessageId.ToString(), received.MessageId);
        Assert.Equal(message.Type, received.Subject);
        Assert.Equal("application/json", received.ContentType);
        Assert.Equal(message.Payload, received.Body.ToString());
        Assert.Equal(occurredAt, DateTimeOffset.Parse((string)received.ApplicationProperties[ServiceBusEventBus.OccurredAtProperty], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The emulator's unfiltered subscription is shared by every messaging test, so messages from neighbouring tests
    /// can be waiting or arrive first. Receive and complete until ours shows up, within a generous deadline.
    /// </summary>
    private static async Task<ServiceBusReceivedMessage> ReceiveByIdAsync(ServiceBusClient client, string messageId)
    {
        await using var receiver = client.CreateReceiver(ServiceBusFixture.Topic, ServiceBusFixture.Subscription);
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var candidate = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            if (candidate is null)
            {
                continue;
            }

            await receiver.CompleteMessageAsync(candidate);
            if (candidate.MessageId == messageId)
            {
                return candidate;
            }
        }

        throw new TimeoutException(FormattableString.Invariant($"Message {messageId} was not delivered in time."));
    }
}
