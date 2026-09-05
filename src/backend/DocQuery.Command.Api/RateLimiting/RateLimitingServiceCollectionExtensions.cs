namespace DocQuery.Command.Api.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddUploadRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<UploadRateLimitOptions>()
            .Bind(configuration.GetSection(UploadRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy<string, UploadRateLimitPolicy>(UploadRateLimitPolicy.Name);
        });

        return services;
    }
}
