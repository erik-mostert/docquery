using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace DocQuery.Command.Api.Tests.Cors;

public sealed class CorsTests(CommandApiFactory factory) : IClassFixture<CommandApiFactory>
{
    private const string AllowedOrigin = "http://localhost:5173";

    [Fact]
    public async Task Preflight_from_allowed_origin_is_permitted()
    {
        using var client = CreateClient();
        using var request = PreflightRequest(AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Preflight_from_other_origin_gets_no_allow_origin_header()
    {
        using var client = CreateClient();
        using var request = PreflightRequest("http://evil.example");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            }))).CreateClient();

    private static HttpRequestMessage PreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, TestContent.DocumentsUri);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }
}
