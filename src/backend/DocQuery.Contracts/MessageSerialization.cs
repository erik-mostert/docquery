using System.Text.Json;

namespace DocQuery.Contracts;

/// <summary>JSON settings shared by every publisher and consumer of the contracts, so payloads round-trip.</summary>
public static class MessageSerialization
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
