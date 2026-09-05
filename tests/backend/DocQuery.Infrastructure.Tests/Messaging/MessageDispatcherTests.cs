using System.Text.Json;
using DocQuery.Application.Abstractions;
using DocQuery.Application.Messaging;
using DocQuery.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Tests.Messaging;

public sealed class MessageDispatcherTests
{
    [Fact]
    public async Task Routes_a_known_subject_to_its_handler_with_the_deserialized_message()
    {
        var received = new ReceivedMessages();
        var (dispatcher, _) = Build(received);

        var handled = await dispatcher.DispatchAsync(typeof(Greeting).FullName, BinaryData.FromString("""{"text":"hello"}"""), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal("hello", Assert.Single(received.Greetings).Text);
    }

    [Fact]
    public async Task Unknown_subject_is_reported_as_not_handled()
    {
        var (dispatcher, _) = Build(new ReceivedMessages());

        var handled = await dispatcher.DispatchAsync("Some.Other.Type", BinaryData.FromString("{}"), CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task Missing_subject_is_reported_as_not_handled()
    {
        var (dispatcher, _) = Build(new ReceivedMessages());

        Assert.False(await dispatcher.DispatchAsync(null, BinaryData.FromString("{}"), CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_body_throws_JsonException()
    {
        var (dispatcher, _) = Build(new ReceivedMessages());

        await Assert.ThrowsAnyAsync<JsonException>(() => dispatcher.DispatchAsync(typeof(Greeting).FullName, BinaryData.FromString("not json"), CancellationToken.None));
    }

    [Fact]
    public async Task Each_dispatch_runs_in_its_own_scope()
    {
        var received = new ReceivedMessages();
        var (dispatcher, _) = Build(received);

        await dispatcher.DispatchAsync(typeof(Greeting).FullName, BinaryData.FromString("""{"text":"a"}"""), CancellationToken.None);
        await dispatcher.DispatchAsync(typeof(Greeting).FullName, BinaryData.FromString("""{"text":"b"}"""), CancellationToken.None);

        Assert.Equal(2, received.HandlerInstances.Distinct().Count());
    }

    [Fact]
    public void Two_routes_for_the_same_subject_are_rejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ReceivedMessages());
        services.AddIntegrationMessageHandler<Greeting, GreetingHandler>();
        services.AddIntegrationMessageHandler<Greeting, GreetingHandler>();
        services.AddSingleton<MessageDispatcher>();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(provider.GetRequiredService<MessageDispatcher>);
    }

    private static (MessageDispatcher Dispatcher, ServiceProvider Provider) Build(ReceivedMessages received)
    {
        var services = new ServiceCollection();
        services.AddSingleton(received);
        services.AddIntegrationMessageHandler<Greeting, GreetingHandler>();
        services.AddSingleton<MessageDispatcher>();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<MessageDispatcher>(), provider);
    }

    public sealed record Greeting(string Text);

    public sealed class ReceivedMessages
    {
        public List<Greeting> Greetings { get; } = [];

        public List<Guid> HandlerInstances { get; } = [];
    }

    public sealed class GreetingHandler(ReceivedMessages received) : IIntegrationMessageHandler<Greeting>
    {
        private readonly Guid _instance = Guid.NewGuid();

        public Task HandleAsync(Greeting message, CancellationToken cancellationToken)
        {
            received.Greetings.Add(message);
            received.HandlerInstances.Add(_instance);
            return Task.CompletedTask;
        }
    }
}
