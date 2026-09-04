using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace DocQuery.Command.Api.Endpoints;

/// <summary>
/// <c>/health</c>: readiness, every registered check must pass. <c>/alive</c>: liveness, only checks tagged
/// "live" (the ServiceDefaults self check). Both are mapped in every environment for Kubernetes probes.
/// </summary>
internal sealed class HealthEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
        });
    }
}
