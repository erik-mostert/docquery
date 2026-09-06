using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocQuery.Query.Api.Tests.Options;

/// <summary>
/// Invalid configuration must stop the host at startup, not surface on the first request. The host is composed
/// exactly as Program does and its startup validator is invoked directly, without starting a server.
/// </summary>
public sealed class OptionsValidationTests
{
    [Theory]
    [InlineData("Querying:DefaultTopK", "0")]
    [InlineData("Querying:MaxTopK", "51")]
    [InlineData("Querying:ExcerptLength", "10")]
    [InlineData("Querying:MaxAnswerTokens", "0")]
    [InlineData("AzureOpenAI:ChatDeployment", "")]
    [InlineData("AzureOpenAI:EmbeddingDeployment", "")]
    [InlineData("RateLimiting:Queries:PermitLimit", "0")]
    [InlineData("RateLimiting:Queries:WindowSeconds", "0")]
    [InlineData("Resilience:AzureOpenAI:FailureRatio", "2")]
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

    [Fact]
    public void Configure_fails_immediately_without_an_openai_connection_string()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(QueryApiFactory.PlaceholderSettings);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:openai"] = null });

        var exception = Assert.Throws<InvalidOperationException>(() => QueryApiHost.ConfigureServices(builder));

        Assert.Contains("ConnectionStrings:openai", exception.Message, StringComparison.Ordinal);
    }

    private static IStartupValidator BuildStartupValidator(Dictionary<string, string?> overrides)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(QueryApiFactory.PlaceholderSettings);
        builder.Configuration.AddInMemoryCollection(overrides);
        QueryApiHost.ConfigureServices(builder);

        var app = builder.Build();
        return app.Services.GetRequiredService<IStartupValidator>();
    }
}
