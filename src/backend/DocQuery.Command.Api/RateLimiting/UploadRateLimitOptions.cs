using System.ComponentModel.DataAnnotations;

namespace DocQuery.Command.Api.RateLimiting;

/// <summary>Bound from <c>RateLimiting:Uploads</c>; validated at startup. Sliding window per client.</summary>
public sealed class UploadRateLimitOptions
{
    public const string SectionName = "RateLimiting:Uploads";

    /// <summary>Uploads allowed per client per window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, 86_400)]
    public int WindowSeconds { get; set; } = 60;

    [Range(1, 1_000)]
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Requests queued when the limit is reached; 0 rejects immediately.</summary>
    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; }
}
