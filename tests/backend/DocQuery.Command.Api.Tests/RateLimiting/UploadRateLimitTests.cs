using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace DocQuery.Command.Api.Tests.RateLimiting;

public sealed class UploadRateLimitTests(CommandApiFactory factory) : IClassFixture<CommandApiFactory>
{
    [Fact]
    public async Task Requests_beyond_permit_limit_get_too_many_requests_with_retry_after()
    {
        using var client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Uploads:PermitLimit"] = "2",
                ["RateLimiting:Uploads:WindowSeconds"] = "60",
                ["RateLimiting:Uploads:QueueLimit"] = "0",
            }))).CreateClient();

        for (var i = 0; i < 2; i++)
        {
            using var form = TestContent.PdfForm();
            using var accepted = await client.PostAsync(TestContent.DocumentsUri, form);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        using var thirdForm = TestContent.PdfForm();
        using var rejected = await client.PostAsync(TestContent.DocumentsUri, thirdForm);

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
                ["RateLimiting:Uploads:PermitLimit"] = "1",
                ["RateLimiting:Uploads:QueueLimit"] = "0",
            }))).CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
