using DocQuery.Contracts.Messaging;
using System.Text.Json;
using DocQuery.Application.Messaging;
using DocQuery.Contracts;

namespace DocQuery.Infrastructure.Persistence.Outbox;

/// <summary>
/// An integration message written in the same transaction as the state change that caused it (ADR 0010).
/// The relay publishes unprocessed rows to the event bus and marks them processed.
/// </summary>
public sealed class OutboxMessage
{
    public static JsonSerializerOptions SerializerOptions => MessageSerialization.Options;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Full name of the contract type, used by consumers to deserialize and route.</summary>
    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Number of failed publish attempts so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>Message of the most recent failed attempt; cleared once published.</summary>
    public string? Error { get; private set; }

    public static OutboxMessage From(object message, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(message);

        var type = message.GetType();

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = MessageSubject.Of(type),
            Payload = JsonSerializer.Serialize(message, type, SerializerOptions),
            OccurredAt = occurredAt,
        };
    }

    public OutboundMessage ToOutbound() => new(Id, Type, Payload, OccurredAt);

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        Error = error;
    }
}
