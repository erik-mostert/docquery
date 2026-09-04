using System.Net;

namespace DocQuery.Command.Api.Tests;

public sealed class HealthEndpointTests(CommandApiFactory factory) : IClassFixture<CommandApiFactory>
{
    [Fact]
    public async Task Get_health_returns_ok_with_healthy_body()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_alive_returns_ok()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/alive", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
