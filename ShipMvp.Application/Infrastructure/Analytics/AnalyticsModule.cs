using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipMvp.Application.Analytics;
using ShipMvp.Application.Infrastructure.Analytics.Data;
using ShipMvp.Application.Infrastructure.Analytics.Services;
using ShipMvp.Core.Abstractions;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
using ShipMvp.Domain.Analytics;

namespace ShipMvp.Application.Infrastructure.Analytics;

[Module]
public sealed class AnalyticsModule : IModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register LLM logging domain services
        services.AddScoped<ILlmLogRepository, LlmLogRepository>();
        services.AddScoped<ILlmLoggingService, LlmLoggingService>();
        
        // Register LLM analytics application services
        services.AddTransient<ILlmAnalyticsService, LlmAnalyticsService>();
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        // No middleware to configure for analytics module
    }
}
