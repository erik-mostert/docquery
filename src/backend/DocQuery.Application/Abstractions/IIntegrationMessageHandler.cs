namespace DocQuery.Application.Abstractions;

/// <summary>
/// Handles one integration message type received from the event bus. Implementations must be idempotent: delivery
/// is at-least-once. Throw <see cref="Exceptions.PermanentProcessingException"/> for failures that a retry cannot fix.
/// </summary>
public interface IIntegrationMessageHandler<in TMessage>
    where TMessage : class
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
