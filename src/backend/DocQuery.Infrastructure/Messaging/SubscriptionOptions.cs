using System.ComponentModel.DataAnnotations;

namespace DocQuery.Infrastructure.Messaging;

/// <summary>Bound from <c>Messaging:Subscription</c> by consumer hosts; validated at startup.</summary>
public sealed class SubscriptionOptions
{
    public const string SectionName = "Messaging:Subscription";

    /// <summary>The subscription on the topic this host consumes from.</summary>
    [Required]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Messages processed concurrently by one instance.</summary>
    [Range(1, 64)]
    public int MaxConcurrentCalls { get; set; } = 4;

    [Range(0, 1000)]
    public int PrefetchCount { get; set; }

    /// <summary>
    /// How long the processor keeps renewing a message lock while a handler runs. Chunking a large PDF can outlast
    /// the one-minute default lock.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan MaxAutoLockRenewalDuration { get; set; } = TimeSpan.FromMinutes(10);
}
