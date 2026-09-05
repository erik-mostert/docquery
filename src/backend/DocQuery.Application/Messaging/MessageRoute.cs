namespace DocQuery.Application.Messaging;

/// <summary>
/// Maps a message subject (the contract type's full name) to its CLR type and a delegate that resolves the handler
/// from a scope and invokes it. Registered by <c>AddIntegrationMessageHandler</c>; consumed by the transport's dispatcher.
/// </summary>
public sealed record MessageRoute(string Subject, Type MessageType, Func<IServiceProvider, object, CancellationToken, Task> Invoke);
