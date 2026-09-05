namespace DocQuery.Application.Messaging;

/// <summary>
/// A serialized integration message ready for the bus. <paramref name="MessageId"/> is the outbox row id so the
/// broker can detect duplicates; <paramref name="Type"/> is the contract's full type name used for routing.
/// </summary>
public sealed record OutboundMessage(Guid MessageId, string Type, string Payload, DateTimeOffset OccurredAt);
