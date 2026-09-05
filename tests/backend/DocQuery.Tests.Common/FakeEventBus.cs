using DocQuery.Application.Abstractions;
using DocQuery.Application.Messaging;

namespace DocQuery.Tests.Common;

/// <summary>Records published messages in memory; a failure script can make calls throw.</summary>
public sealed class FakeEventBus : IEventBus
{
    private readonly List<OutboundMessage> _published = [];
    private readonly Queue<Exception> _failures = new();
    private readonly Lock _gate = new();

    public IReadOnlyList<OutboundMessage> Published
    {
        get
        {
            lock (_gate)
            {
                return _published.ToArray();
            }
        }
    }

    public int CallCount { get; private set; }

    public void FailNextCallWith(Exception exception) => _failures.Enqueue(exception);

    public Task PublishAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            CallCount++;

            if (_failures.TryDequeue(out var failure))
            {
                throw failure;
            }

            _published.Add(message);
        }

        return Task.CompletedTask;
    }
}
