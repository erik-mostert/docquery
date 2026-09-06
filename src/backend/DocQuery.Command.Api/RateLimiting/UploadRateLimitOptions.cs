using DocQuery.Api.Common.RateLimiting;

namespace DocQuery.Command.Api.RateLimiting;

/// <summary>Bound from <c>RateLimiting:Uploads</c>; validated at startup. Sliding window per client.</summary>
public sealed class UploadRateLimitOptions : SlidingWindowRateLimitOptions
{
    public const string SectionName = "RateLimiting:Uploads";

    /// <summary>Policy name endpoints opt into with <c>RequireRateLimiting</c>.</summary>
    public const string PolicyName = "uploads";

    public override string RejectionTitle => "Too many uploads";
}
