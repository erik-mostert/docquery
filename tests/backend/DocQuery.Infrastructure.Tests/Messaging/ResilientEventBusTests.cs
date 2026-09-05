using DocQuery.Application.Abstractions;
using DocQuery.Application.Messaging;
using DocQuery.Infrastructure;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;

namespace DocQuery.Infrastructure.Tests.Messaging;

public sealed class ResilientEventBusTests
{
    private static readonly OutboundMessage Message = new(Guid.NewGuid(), "Test.Event", "{}", DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Retries_transient_failure()
    {
        var (bus, inner) = Build(maxRetryAttempts: 3);
        inner.FailNextCallWith(new IOException("blip"));

        await bus.PublishAsync(Message, CancellationToken.None);

        Assert.Equal(2, inner.CallCount);
        Assert.Single(inner.Published);
    }

    [Fact]
    public async Task Gives_up_after_max_retry_attempts()
    {
        var (bus, inner) = Build(maxRetryAttempts: 2);
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new IOException("down"));
        }

        await Assert.ThrowsAsync<IOException>(() => bus.PublishAsync(Message, CancellationToken.None));

        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task Opens_circuit_after_failure_threshold()
    {
        var (bus, inner) = Build(maxRetryAttempts: 0, minimumThroughput: 2, failureRatio: 1.0);
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new IOException("down"));
        }

        for (var i = 0; i < 2; i++)
        {
            await Assert.ThrowsAsync<IOException>(() => bus.PublishAsync(Message, CancellationToken.None));
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(() => bus.PublishAsync(Message, CancellationToken.None));
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Does_not_retry_cancellation()
    {
        var (bus, inner) = Build(maxRetryAttempts: 3);
        inner.FailNextCallWith(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => bus.PublishAsync(Message, CancellationToken.None));

        Assert.Equal(1, inner.CallCount);
    }

    private static (IEventBus Bus, FakeEventBus Inner) Build(int maxRetryAttempts, int minimumThroughput = 100, double failureRatio = 1.0)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:ServiceBus:MaxRetryAttempts"] = maxRetryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:ServiceBus:BaseDelay"] = "00:00:00.001",
                ["Resilience:ServiceBus:FailureRatio"] = failureRatio.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:ServiceBus:MinimumThroughput"] = minimumThroughput.ToString(System.Globalization.CultureInfo.InvariantCulture),
            })
            .Build();

        var inner = new FakeEventBus();
        var services = new ServiceCollection();
        services.AddServiceBusResilience(configuration);
        services.AddResilientEventBus();
        services.AddKeyedSingleton<IEventBus>(ServiceKeys.RawEventBus, inner);

        return (services.BuildServiceProvider().GetRequiredService<IEventBus>(), inner);
    }
}
