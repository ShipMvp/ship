using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ShipMvp.Core.EventBus;

public class LocalDistributedEventBus : IDistributedEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, Type> _handlers = new();

    public LocalDistributedEventBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : class
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var handlerType))
        {
            // Resolve handler from a scope so scoped/transient dependencies are honored.
            using var scope = _serviceProvider.CreateScope();
            var handler = (IDistributedEventHandler<TEvent>?)scope.ServiceProvider.GetService(handlerType);
            if (handler != null)
            {
                // Await handler to ensure scoped services (like DbContext) remain alive until handling completes.
                await handler.HandleAsync(@event);
            }
        }

        return;
    }

    public Task SubscribeAsync<TEvent, THandler>()
        where TEvent : class
        where THandler : IDistributedEventHandler<TEvent>
    {
        _handlers[typeof(TEvent)] = typeof(THandler);
        return Task.CompletedTask;
    }
}
