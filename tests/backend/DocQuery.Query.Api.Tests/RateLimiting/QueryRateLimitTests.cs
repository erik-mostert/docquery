using System.Net;
using System.Net.Http.Json;
using DocQuery.Query.Api.Endpoints.Queries;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace DocQuery.Query.Api.Tests.RateLimiting;

public sealed class QueryRateLimitTests(QueryApiFactory factory) : IClassFixture<QueryApiFactory>
{
    private static readonly Uri QueriesUri = new("/queries", UriKind.Relative);

    [Fact]
    public async Task Requests_beyond_permit_limit_get_too_many_requests_with_retry_after()
    {
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Queries:PermitLimit"] = "2",
                ["RateLimiting:Queries:WindowSeconds"] = "60",
                ["RateLimiting:Queries:QueueLimit"] = "0",
            }))).CreateClient();

        for (var i = 0; i < 2; i++)
        {
            using var ok = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("question"));
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync(QueriesUri, new AskQuestionRequest("question"));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Health_endpoint_is_not_rate_limited()
    {
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Queries:PermitLimit"] = "1",
                ["RateLimiting:Queries:QueueLimit"] = "0",
            }))).CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
