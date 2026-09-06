using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Api.Common.Cors;

public static class CorsServiceCollectionExtensions
{
    /// <summary>Default CORS policy allowing the configured origins with any header and method, no credentials.</summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var origins = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.AllowedOrigins ?? [];

        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins([.. origins])
            .AllowAnyHeader()
            .AllowAnyMethod()));

        return services;
    }
}
