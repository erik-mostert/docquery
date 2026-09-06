using System.ClientModel;
using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Query.Api.ExceptionHandling;

/// <summary>
/// Maps a failure reported by the Azure OpenAI client, after the resilience pipeline has exhausted its retries, to
/// <c>502 Bad Gateway</c> with problem details and a <c>Retry-After</c> hint. The upstream message is logged by the
/// exception handler middleware but not echoed to callers.
/// </summary>
internal sealed class AiServiceExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>Azure OpenAI blips (deployment re-routing, throttling) usually clear within tens of seconds.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ClientResultException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        httpContext.Response.Headers.RetryAfter = Math.Ceiling(RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "AI service error",
                Detail = "The language model service did not complete the request. Retry after the interval in the Retry-After header.",
            },
        });
    }
}
