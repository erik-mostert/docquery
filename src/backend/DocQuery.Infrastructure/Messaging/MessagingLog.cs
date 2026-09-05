using Microsoft.Extensions.Logging;

namespace DocQuery.Infrastructure.Messaging;

internal static partial class MessagingLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Listening on {Topic}/{Subscription} with up to {MaxConcurrentCalls} concurrent messages.")]
    public static partial void ListenerStarted(ILogger logger, string topic, string subscription, int maxConcurrentCalls);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Handled message {MessageId} ({Subject}).")]
    public static partial void MessageHandled(ILogger logger, string messageId, string? subject);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Ignoring message {MessageId}: no handler for subject '{Subject}'.")]
    public static partial void UnknownSubject(ILogger logger, string messageId, string? subject);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Dead-lettering message {MessageId} ({Subject}): {Reason}: {Description}")]
    public static partial void DeadLettered(ILogger logger, string messageId, string? subject, string reason, string description);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Message {MessageId} ({Subject}) failed on delivery {DeliveryCount}; abandoning for redelivery.")]
    public static partial void TransientFailure(ILogger logger, Exception exception, string messageId, string? subject, int deliveryCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Could not settle message {MessageId}; the lock was probably lost and the message will be redelivered.")]
    public static partial void SettlementFailed(ILogger logger, Exception exception, string messageId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Service Bus processor error from {ErrorSource} on {EntityPath}.")]
    public static partial void ProcessorError(ILogger logger, Exception exception, string errorSource, string entityPath);
}
