namespace DocQuery.Contracts.Messaging;

/// <summary>
/// The Service Bus <c>Subject</c> of an integration message: the contract type's full name. Publishers stamp it,
/// consumers route on it, and subscriptions filter on it (ADR 0018), so it is computed in one place.
/// </summary>
public static class MessageSubject
{
    public static string Of<TMessage>() => Of(typeof(TMessage));

    public static string Of(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return messageType.FullName ?? messageType.Name;
    }

    public static string Of(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Of(message.GetType());
    }
}
