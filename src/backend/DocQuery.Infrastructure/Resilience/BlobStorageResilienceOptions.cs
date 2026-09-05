using System.ComponentModel.DataAnnotations;

namespace DocQuery.Infrastructure.Resilience;

/// <summary>
/// Bound from <c>Resilience:BlobStorage</c> and validated at startup. Ranges mirror Polly's own limits so a bad
/// value fails the host rather than the first request. Defaults suit Azure Blob Storage throttling behaviour.
/// </summary>
public sealed class BlobStorageResilienceOptions
{
    public const string SectionName = "Resilience:BlobStorage";

    /// <summary>Retries after the first attempt. Total attempts = this + 1. Zero disables retry.</summary>
    [Range(0, 100)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff; jitter is always applied.</summary>
    [Range(typeof(TimeSpan), "00:00:00.001", "00:10:00")]
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Timeout for a single attempt.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "01:00:00")]
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Failure ratio within <see cref="SamplingDuration"/> that opens the circuit. Polly requires (0, 1].</summary>
    [Range(0.001, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Polly requires at least 500 ms.</summary>
    [Range(typeof(TimeSpan), "00:00:00.500", "1.00:00:00")]
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Minimum calls within the sampling window before the ratio is evaluated. Polly requires at least 2.</summary>
    [Range(2, int.MaxValue)]
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>How long the circuit stays open before a probe call is allowed. Polly requires at least 500 ms.</summary>
    [Range(typeof(TimeSpan), "00:00:00.500", "1.00:00:00")]
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
