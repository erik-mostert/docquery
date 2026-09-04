namespace DocQuery.Command.Api.Endpoints;

/// <summary>One class per endpoint. Implementations are discovered by <see cref="EndpointExtensions.AddEndpoints"/>.</summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
