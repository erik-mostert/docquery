using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;

namespace DocQuery.Api.Common.ExceptionHandling;

/// <summary>
/// Maps an open circuit on an outbound dependency to <c>503 Service Unavailable</c> with a <c>Retry-After</c>
/// header, so callers back off instead of hammering a dependency that is already failing (ADR 0007).
/// </summary>
public sealed class BrokenCircuitExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>Matches the default circuit break duration; the exception does not carry the remaining break time.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BrokenCircuitException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = Math.Ceiling(RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service temporarily unavailable",
                Detail = "A dependency is unavailable. Retry after the interval in the Retry-After header.",
            },
        });
    }
}
