using DocQuery.Infrastructure.AI;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Tests.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace DocQuery.Infrastructure.Tests.AI;

public sealed class ResilientChatClientTests
{
    private static readonly ChatMessage[] Messages = [new(ChatRole.System, "system"), new(ChatRole.User, "question")];

    [Fact]
    public async Task Retries_transient_failure_with_the_same_messages()
    {
        using var inner = new FakeChatClient { Answer = "answer" };
        inner.FailNextCallWith(new HttpRequestException("429"));
        using var client = Build(inner, maxRetryAttempts: 3);

        var response = await client.GetResponseAsync(Messages);

        Assert.Equal("answer", response.Text);
        Assert.Equal(2, inner.Requests.Count);
        Assert.All(inner.Requests, request => Assert.Equal(Messages.Select(m => m.Text), request.Select(m => m.Text)));
    }

    [Fact]
    public async Task Gives_up_after_max_retry_attempts()
    {
        using var inner = new FakeChatClient();
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new HttpRequestException("down"));
        }

        using var client = Build(inner, maxRetryAttempts: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync(Messages));
        Assert.Equal(3, inner.Requests.Count);
    }

    [Fact]
    public async Task Opens_circuit_after_failure_threshold()
    {
        using var inner = new FakeChatClient();
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new HttpRequestException("down"));
        }

        using var client = Build(inner, maxRetryAttempts: 0, minimumThroughput: 2, failureRatio: 1.0);

        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync(Messages));
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.GetResponseAsync(Messages));
        Assert.Equal(2, inner.Requests.Count);
    }

    [Fact]
    public async Task Does_not_retry_cancellation()
    {
        using var inner = new FakeChatClient();
        inner.FailNextCallWith(new OperationCanceledException());
        using var client = Build(inner, maxRetryAttempts: 3);

        await Assert.ThrowsAsync<OperationCanceledException>(() => client.GetResponseAsync(Messages));
        Assert.Single(inner.Requests);
    }

    private static ResilientChatClient Build(
        FakeChatClient inner,
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

        return new ResilientChatClient(inner, provider.GetRequiredService<ResiliencePipelineProvider<string>>());
    }
}
