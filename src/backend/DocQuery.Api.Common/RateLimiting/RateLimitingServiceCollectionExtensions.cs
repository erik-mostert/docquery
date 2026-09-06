using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Api.Common.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> from <paramref name="sectionName"/> (validated at startup) and registers
    /// a per-client sliding-window policy under <paramref name="policyName"/> for endpoints to opt into with
    /// <c>RequireRateLimiting</c>.
    /// </summary>
    public static IServiceCollection AddClientRateLimitPolicy<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string policyName,
        string sectionName)
        where TOptions : SlidingWindowRateLimitOptions
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy<string, ClientRateLimitPolicy<TOptions>>(policyName);
        });

        return services;
    }
}
