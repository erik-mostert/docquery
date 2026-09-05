using DocQuery.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DocQuery.Application.Messaging;

public static class MessageHandlerServiceCollectionExtensions
{
    /// <summary>Registers <typeparamref name="THandler"/> for <typeparamref name="TMessage"/> and the route the dispatcher uses to reach it.</summary>
    public static IServiceCollection AddIntegrationMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IIntegrationMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IIntegrationMessageHandler<TMessage>, THandler>();
        services.AddSingleton(new MessageRoute(
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            typeof(TMessage),
            static (provider, message, cancellationToken) =>
                provider.GetRequiredService<IIntegrationMessageHandler<TMessage>>().HandleAsync((TMessage)message, cancellationToken)));

        return services;
    }
}
