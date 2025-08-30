using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShipMvp.Core.EventBus;

public static class EventBusServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedEventBus(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedEventBus, LocalDistributedEventBus>();
        return services;
    }
}
