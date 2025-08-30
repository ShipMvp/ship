using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipMvp.Core.EventBus;

namespace ShipMvp.Application.Infrastructure.EventBus;

public static class EventBusServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        var useGcp = configuration["EventBus:Provider"] == "GcpPubsub";
        if (useGcp)
        {
            services.AddSingleton<IDistributedEventBus, GcpPubsubDistributedEventBus>();
        }
        else
        {
            services.AddSingleton<IDistributedEventBus, LocalDistributedEventBus>();
        }
        return services;
    }
}
