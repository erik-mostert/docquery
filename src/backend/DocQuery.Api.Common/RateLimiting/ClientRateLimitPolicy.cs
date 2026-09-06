using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocQuery.Api.Common.RateLimiting;

/// <summary>
/// Sliding-window limit partitioned by client IP, configured by <typeparamref name="TOptions"/>. Behind an ingress,
/// forwarded headers must be configured so the partition key is the real client rather than the proxy (ADR 0008).
/// </summary>
public sealed class ClientRateLimitPolicy<TOptions>(IOptions<TOptions> options) : IRateLimiterPolicy<string>
    where TOptions : SlidingWindowRateLimitOptions
{
    private const string UnknownClient = "unknown";

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } =
        (context, cancellationToken) => WriteRejectionAsync(context, options.Value, cancellationToken);

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var settings = options.Value;
        var client = httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

        return RateLimitPartition.GetSlidingWindowLimiter(client, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            SegmentsPerWindow = settings.SegmentsPerWindow,
            QueueLimit = settings.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, TOptions settings, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        // The sliding-window lease only carries Retry-After metadata for queued requests; fall back to the window.
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var fromLease)
            ? fromLease
            : TimeSpan.FromSeconds(settings.WindowSeconds);
        response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = settings.RejectionTitle,
                Detail = "The rate limit was exceeded. Retry after the interval in the Retry-After header.",
            },
        });
    }
}
