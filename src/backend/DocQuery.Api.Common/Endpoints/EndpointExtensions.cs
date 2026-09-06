using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocQuery.Api.Common.Endpoints;

public static class EndpointExtensions
{
    /// <summary>Registers every non-abstract <see cref="IEndpoint"/> in <paramref name="assembly"/>.</summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        var descriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IEndpoint).IsAssignableFrom(type))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type));

        services.TryAddEnumerable(descriptors);

        return services;
    }

    /// <summary>Registers the endpoints every API host exposes (health probes).</summary>
    public static IServiceCollection AddCommonEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Transient<IEndpoint, HealthEndpoint>());

        return services;
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.Map(app);
        }

        return app;
    }
}
