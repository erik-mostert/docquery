using System.Collections.Frozen;
using System.Text.Json;
using DocQuery.Application.Messaging;
using DocQuery.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Infrastructure.Messaging;

/// <summary>
/// Routes a received message to its handler by subject (the contract type's full name). Deserializes with the
/// shared contract settings and runs each message in its own DI scope. Transport-agnostic apart from the body type.
/// </summary>
internal sealed class MessageDispatcher
{
    private readonly FrozenDictionary<string, MessageRoute> _routes;
    private readonly IServiceScopeFactory _scopeFactory;

    public MessageDispatcher(IEnumerable<MessageRoute> routes, IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var table = new Dictionary<string, MessageRoute>(StringComparer.Ordinal);
        foreach (var route in routes)
        {
            if (!table.TryAdd(route.Subject, route))
            {
                throw new InvalidOperationException(FormattableString.Invariant($"Two message handlers are registered for subject '{route.Subject}'."));
            }
        }

        _routes = table.ToFrozenDictionary(StringComparer.Ordinal);
        _scopeFactory = scopeFactory;
    }

    /// <returns><see langword="false"/> when no handler is registered for the subject; the caller decides what that means.</returns>
    /// <exception cref="JsonException">The body is not a valid instance of the routed contract.</exception>
    public async Task<bool> DispatchAsync(string? subject, BinaryData body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (subject is null || !_routes.TryGetValue(subject, out var route))
        {
            return false;
        }

        var message = JsonSerializer.Deserialize(body.ToMemory().Span, route.MessageType, MessageSerialization.Options)
            ?? throw new JsonException("The message body deserialized to null.");

        await using var scope = _scopeFactory.CreateAsyncScope();
        await route.Invoke(scope.ServiceProvider, message, cancellationToken);

        return true;
    }
}
