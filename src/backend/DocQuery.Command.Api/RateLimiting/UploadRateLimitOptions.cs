namespace DocQuery.Command.Api.RateLimiting;

/// <summary>Bound from <c>RateLimiting:Uploads</c>. Sliding window per client.</summary>
public sealed class UploadRateLimitOptions
{
    public const string SectionName = "RateLimiting:Uploads";

    /// <summary>Uploads allowed per client per window.</summary>
    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;

    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Requests queued when the limit is reached; 0 rejects immediately.</summary>
    public int QueueLimit { get; set; }
}
