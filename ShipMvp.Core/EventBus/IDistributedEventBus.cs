using System.Threading.Tasks;

namespace ShipMvp.Core.EventBus;

public interface IDistributedEventBus
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : class;
    Task SubscribeAsync<TEvent, THandler>() where TEvent : class where THandler : IDistributedEventHandler<TEvent>;
}

public interface IDistributedEventHandler<TEvent> where TEvent : class
{
    Task HandleAsync(TEvent @event);
}
