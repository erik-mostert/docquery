namespace DocQuery.Infrastructure.Resilience;

/// <summary>Bound from <c>Resilience:BlobStorage</c>. Defaults suit Azure Blob Storage throttling behaviour.</summary>
public sealed class BlobStorageResilienceOptions
{
    public const string SectionName = "Resilience:BlobStorage";

    /// <summary>Retries after the first attempt. Total attempts = this + 1. Zero disables retry.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff; jitter is always applied.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout for a single attempt.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Failure ratio within <see cref="SamplingDuration"/> that opens the circuit.</summary>
    public double FailureRatio { get; set; } = 0.5;

    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Minimum calls within the sampling window before the ratio is evaluated.</summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>How long the circuit stays open before a probe call is allowed.</summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
