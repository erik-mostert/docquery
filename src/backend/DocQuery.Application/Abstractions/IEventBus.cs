using DocQuery.Application.Messaging;

namespace DocQuery.Application.Abstractions;

/// <summary>Publishes integration messages to the event bus. Implemented by Azure Service Bus in Infrastructure (ADR 0004).</summary>
public interface IEventBus
{
    Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken);
}
