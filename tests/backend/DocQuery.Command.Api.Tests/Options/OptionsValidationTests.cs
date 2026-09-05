using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DocQuery.Command.Api.Tests.Options;

/// <summary>Invalid configuration must stop the host at startup, not surface on the first request.</summary>
public sealed class OptionsValidationTests(CommandApiFactory factory) : IClassFixture<CommandApiFactory>
{
    [Theory]
    [InlineData("Upload:MaxFileSizeBytes", "0")]
    [InlineData("RateLimiting:Uploads:PermitLimit", "0")]
    [InlineData("RateLimiting:Uploads:WindowSeconds", "0")]
    [InlineData("RateLimiting:Uploads:SegmentsPerWindow", "0")]
    [InlineData("RateLimiting:Uploads:QueueLimit", "-1")]
    [InlineData("Resilience:BlobStorage:FailureRatio", "2")]
    public void Host_fails_to_start_with_out_of_range_setting(string key, string value)
    {
        using var invalid = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [key] = value,
            })));

        var exception = Record.Exception(() => invalid.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(key.Split(':')[^1], FindOptionsValidation(exception).Message, StringComparison.Ordinal);
    }

    private static OptionsValidationException FindOptionsValidation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OptionsValidationException validation)
            {
                return validation;
            }
        }

        throw new InvalidOperationException($"No OptionsValidationException in chain: {exception}");
    }
}
