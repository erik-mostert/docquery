using Microsoft.Extensions.AI;

namespace DocQuery.Tests.Common;

/// <summary>Returns a scripted answer and records every request; can be told to fail the next call.</summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly Queue<Exception> _failures = new();

    public string Answer { get; set; } = "The answer. [1]";

    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public void FailNextCallWith(Exception exception) => _failures.Enqueue(exception);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Requests.Add(messages.ToList());
        Options.Add(options);

        if (_failures.TryDequeue(out var failure))
        {
            throw failure;
        }

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Answer)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType?.IsInstanceOfType(this) == true ? this : null;

    public void Dispose()
    {
    }
}
