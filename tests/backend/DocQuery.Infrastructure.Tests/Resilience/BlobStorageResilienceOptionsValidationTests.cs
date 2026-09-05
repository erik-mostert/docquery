using DocQuery.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Tests.Resilience;

public sealed class BlobStorageResilienceOptionsValidationTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var options = Resolve([]);

        Assert.Equal(3, options.Value.MaxRetryAttempts);
    }

    [Theory]
    [InlineData("MaxRetryAttempts", "-1")]
    [InlineData("BaseDelay", "00:00:00")]
    [InlineData("AttemptTimeout", "00:00:00")]
    [InlineData("FailureRatio", "0")]
    [InlineData("FailureRatio", "1.5")]
    [InlineData("SamplingDuration", "00:00:00.100")]
    [InlineData("MinimumThroughput", "1")]
    [InlineData("BreakDuration", "00:00:00.100")]
    public void Out_of_range_value_fails_validation(string key, string value)
    {
        var options = Resolve(new Dictionary<string, string?> { [$"Resilience:BlobStorage:{key}"] = value });

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    private static IOptions<BlobStorageResilienceOptions> Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddBlobStorageResilience(configuration);

        return services.BuildServiceProvider().GetRequiredService<IOptions<BlobStorageResilienceOptions>>();
    }
}
