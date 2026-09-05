using System.ComponentModel.DataAnnotations;

namespace DocQuery.Infrastructure.Persistence.Outbox;

/// <summary>Bound from the <c>Outbox</c> configuration section; validated at startup.</summary>
public sealed class OutboxRelayOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the relay looks for pending messages when the last batch was not full.</summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:05:00")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Messages taken per batch. A full batch is followed immediately by another until the table drains.</summary>
    [Range(1, 1_000)]
    public int BatchSize { get; set; } = 50;
}
