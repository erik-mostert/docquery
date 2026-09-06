using System.ComponentModel.DataAnnotations;

namespace DocQuery.Api.Common.RateLimiting;

/// <summary>
/// Sliding window per client. Each endpoint family derives its own options class (bound from its own section) so
/// limits are configured and validated independently (ADR 0008).
/// </summary>
public abstract class SlidingWindowRateLimitOptions
{
    /// <summary>Requests allowed per client per window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; } = 60;

    [Range(1, 1_000)]
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Requests queued when the limit is reached; 0 rejects immediately.</summary>
    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; }

    /// <summary>Problem-details title written on rejection, e.g. "Too many uploads".</summary>
    public abstract string RejectionTitle { get; }
}
