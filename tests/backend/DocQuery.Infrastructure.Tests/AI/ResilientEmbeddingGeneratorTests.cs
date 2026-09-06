using DocQuery.Infrastructure.AI;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace DocQuery.Infrastructure.Tests.AI;

public sealed class ResilientEmbeddingGeneratorTests
{
    private static readonly string[] Inputs = ["alpha", "beta"];

    [Fact]
    public async Task Retries_transient_failure_with_the_same_inputs()
    {
        using var inner = new FakeEmbeddingGenerator();
        inner.FailNextCallWith(new HttpRequestException("429"));
        using var generator = Build(inner, maxRetryAttempts: 3);

        var result = await generator.GenerateAsync(Inputs);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, inner.Batches.Count);
        Assert.All(inner.Batches, batch => Assert.Equal(Inputs, batch));
    }

    [Fact]
    public async Task Gives_up_after_max_retry_attempts()
    {
        using var inner = new FakeEmbeddingGenerator();
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new HttpRequestException("down"));
        }

        using var generator = Build(inner, maxRetryAttempts: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() => generator.GenerateAsync(Inputs));
        Assert.Equal(3, inner.Batches.Count);
    }

    [Fact]
    public async Task Opens_circuit_after_failure_threshold()
    {
        using var inner = new FakeEmbeddingGenerator();
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new HttpRequestException("down"));
        }

        using var generator = Build(inner, maxRetryAttempts: 0, minimumThroughput: 2, failureRatio: 1.0);

        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => generator.GenerateAsync(Inputs));
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(() => generator.GenerateAsync(Inputs));
        Assert.Equal(2, inner.Batches.Count);
    }

    [Fact]
    public async Task Does_not_retry_cancellation()
    {
        using var inner = new FakeEmbeddingGenerator();
        inner.FailNextCallWith(new OperationCanceledException());
        using var generator = Build(inner, maxRetryAttempts: 3);

        await Assert.ThrowsAsync<OperationCanceledException>(() => generator.GenerateAsync(Inputs));
        Assert.Single(inner.Batches);
    }

    private static ResilientEmbeddingGenerator Build(
        FakeEmbeddingGenerator inner,
        int maxRetryAttempts,
        int minimumThroughput = 100,
        double failureRatio = 1.0)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:AzureOpenAI:MaxRetryAttempts"] = maxRetryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:AzureOpenAI:BaseDelay"] = "00:00:00.001",
                ["Resilience:AzureOpenAI:FailureRatio"] = failureRatio.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:AzureOpenAI:MinimumThroughput"] = minimumThroughput.ToString(System.Globalization.CultureInfo.InvariantCulture),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddAzureOpenAIResilience(configuration);
        var provider = services.BuildServiceProvider();

        return new ResilientEmbeddingGenerator(inner, provider.GetRequiredService<ResiliencePipelineProvider<string>>());
    }
}
