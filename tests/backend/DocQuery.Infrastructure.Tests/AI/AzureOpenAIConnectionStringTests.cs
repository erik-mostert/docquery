using DocQuery.Infrastructure.AI;

namespace DocQuery.Infrastructure.Tests.AI;

public sealed class AzureOpenAIConnectionStringTests
{
    [Fact]
    public void Parses_endpoint_and_key()
    {
        var parsed = AzureOpenAIConnectionString.Parse("Endpoint=https://demo.openai.azure.com/;Key=abc123");

        Assert.Equal(new Uri("https://demo.openai.azure.com/"), parsed.Endpoint);
        Assert.Equal("abc123", parsed.Key);
    }

    [Fact]
    public void Endpoint_without_key_means_identity_based_authentication()
    {
        var parsed = AzureOpenAIConnectionString.Parse("Endpoint=https://demo.openai.azure.com/");

        Assert.Null(parsed.Key);
    }

    [Fact]
    public void Segment_order_case_and_whitespace_do_not_matter()
    {
        var parsed = AzureOpenAIConnectionString.Parse(" key=k ; ENDPOINT=https://demo.openai.azure.com/ ;");

        Assert.Equal("k", parsed.Key);
        Assert.Equal("demo.openai.azure.com", parsed.Endpoint.Host);
    }

    [Theory]
    [InlineData("Key=abc")]
    [InlineData("Endpoint=http://insecure.example/;Key=abc")]
    [InlineData("Endpoint=not a url")]
    [InlineData("Endpoint=https://demo.openai.azure.com/;Region=westeurope")]
    [InlineData("garbage")]
    public void Rejects_malformed_values(string value)
    {
        Assert.Throws<FormatException>(() => AzureOpenAIConnectionString.Parse(value));
    }

    [Fact]
    public void Rejects_blank_value()
    {
        Assert.ThrowsAny<ArgumentException>(() => AzureOpenAIConnectionString.Parse("  "));
    }
}
