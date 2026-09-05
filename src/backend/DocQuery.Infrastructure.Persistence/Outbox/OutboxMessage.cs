using System.Text.Json;

namespace DocQuery.Infrastructure.Persistence.Outbox;

/// <summary>
/// An integration message written in the same transaction as the state change that caused it (ADR 0010).
/// A relay publishes unprocessed rows to the event bus and marks them processed.
/// </summary>
public sealed class OutboxMessage
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Full name of the contract type, used by the relay to deserialize and route.</summary>
    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage From(object message, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(message);

        var type = message.GetType();

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type.FullName ?? type.Name,
            Payload = JsonSerializer.Serialize(message, type, SerializerOptions),
            OccurredAt = occurredAt,
        };
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
    }

    public void MarkFailed(string error) => Error = error;
}
