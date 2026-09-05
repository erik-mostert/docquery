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

        await using var receiver = client.CreateReceiver(ServiceBusFixture.Topic, ServiceBusFixture.Subscription);
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(received);
        await receiver.CompleteMessageAsync(received);

        Assert.Equal(message.MessageId.ToString(), received.MessageId);
        Assert.Equal(message.Type, received.Subject);
        Assert.Equal("application/json", received.ContentType);
        Assert.Equal(message.Payload, received.Body.ToString());
        Assert.Equal(occurredAt, DateTimeOffset.Parse((string)received.ApplicationProperties[ServiceBusEventBus.OccurredAtProperty], CultureInfo.InvariantCulture));
    }
}
