using DocQuery.Api.Common.RateLimiting;

namespace DocQuery.Query.Api.RateLimiting;

/// <summary>Bound from <c>RateLimiting:Queries</c>; validated at startup. Sliding window per client.</summary>
public sealed class QueryRateLimitOptions : SlidingWindowRateLimitOptions
{
    public const string SectionName = "RateLimiting:Queries";

    /// <summary>Policy name endpoints opt into with <c>RequireRateLimiting</c>.</summary>
    public const string PolicyName = "queries";

    public override string RejectionTitle => "Too many questions";
}
