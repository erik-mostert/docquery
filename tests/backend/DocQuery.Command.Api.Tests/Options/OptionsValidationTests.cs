using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocQuery.Command.Api.Tests.Options;

/// <summary>
/// Invalid configuration must stop the host at startup, not surface on the first request. The host is composed
/// exactly as Program does and its startup validator is invoked directly, without starting a server, so the
/// outcome does not depend on how a test server surfaces a failed start.
/// </summary>
public sealed class OptionsValidationTests
{
    [Theory]
    [InlineData("Upload:MaxFileSizeBytes", "0")]
    [InlineData("RateLimiting:Uploads:PermitLimit", "0")]
    [InlineData("RateLimiting:Uploads:WindowSeconds", "0")]
    [InlineData("RateLimiting:Uploads:SegmentsPerWindow", "0")]
    [InlineData("RateLimiting:Uploads:QueueLimit", "-1")]
    [InlineData("Resilience:BlobStorage:FailureRatio", "2")]
    public void Startup_validation_rejects_out_of_range_setting(string key, string value)
    {
        var validator = BuildStartupValidator(new Dictionary<string, string?> { [key] = value });

        var exception = Assert.Throws<OptionsValidationException>(validator.Validate);

        Assert.Contains(key.Split(':')[^1], exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_validation_accepts_default_settings()
    {
        var validator = BuildStartupValidator([]);

        validator.Validate();
    }

    private static IStartupValidator BuildStartupValidator(Dictionary<string, string?> overrides)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(CommandApiFactory.PlaceholderSettings);
        builder.Configuration.AddInMemoryCollection(overrides);
        CommandApiHost.ConfigureServices(builder);

        var app = builder.Build();
        return app.Services.GetRequiredService<IStartupValidator>();
    }
}
