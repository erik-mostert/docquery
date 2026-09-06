using Microsoft.AspNetCore.Routing;

namespace DocQuery.Api.Common.Endpoints;

/// <summary>One class per endpoint (ADR 0009). Implementations are discovered by <see cref="EndpointExtensions.AddEndpoints"/>.</summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
