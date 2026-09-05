using Microsoft.Extensions.Logging;

namespace DocQuery.Infrastructure.Persistence.Outbox;

internal static partial class OutboxLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Published {Published} of {Taken} outbox messages.")]
    public static partial void BatchPublished(ILogger logger, int published, int taken);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Publishing outbox message {MessageId} ({MessageType}) failed; attempt {Attempts}. It stays pending.")]
    public static partial void PublishFailed(ILogger logger, Exception exception, Guid messageId, string messageType, int attempts);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Outbox relay cycle failed; will retry on the next tick.")]
    public static partial void RelayCycleFailed(ILogger logger, Exception exception);
}
