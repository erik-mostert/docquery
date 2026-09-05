using System.Globalization;
using Azure.Messaging.ServiceBus;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Messaging;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Messaging;

/// <summary>
/// Raw Azure Service Bus adapter. The outbox id becomes <c>MessageId</c> (duplicate detection), the contract type
/// name becomes <c>Subject</c> (routing), the JSON payload is the body. Registered under
/// <see cref="ServiceKeys.RawEventBus"/> and wrapped by <see cref="ResilientEventBus"/>.
/// </summary>
internal sealed class ServiceBusEventBus : IEventBus, IAsyncDisposable
{
    public const string OccurredAtProperty = "OccurredAt";

    private const string JsonContentType = "application/json";

    private readonly ServiceBusSender _sender;

    public ServiceBusEventBus(ServiceBusClient client, IOptions<MessagingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _sender = client.CreateSender(options.Value.TopicName);
    }

    public Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
        {
            MessageId = message.MessageId.ToString(),
            Subject = message.Type,
            ContentType = JsonContentType,
        };
        serviceBusMessage.ApplicationProperties[OccurredAtProperty] = message.OccurredAt.ToString("O", CultureInfo.InvariantCulture);

        return _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
