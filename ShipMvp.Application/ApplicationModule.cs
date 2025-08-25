using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShipMvp.Domain.Identity;
using ShipMvp.Domain.Subscriptions;
using ShipMvp.Domain.Email;
using ShipMvp.Domain.Email.Templates;
using ShipMvp.Domain.Analytics;
using ShipMvp.Domain.Files;
using ShipMvp.Domain.Integrations;
using ShipMvp.Application.Integrations;
using ShipMvp.Application.Infrastructure.Data;
using ShipMvp.Application.Infrastructure.Subscriptions;
using ShipMvp.Application.Infrastructure.Email.Services;
using ShipMvp.Application.Infrastructure.Email.Templates;
using ShipMvp.Application.Infrastructure.Email.Configuration;
using ShipMvp.Application.Infrastructure.Analytics.Services;
using ShipMvp.Application.Infrastructure.Analytics.Configuration;
using ShipMvp.Application.Infrastructure.Files;
using ShipMvp.Application.Infrastructure.Security;
using ShipMvp.Application.Subscriptions;
using ShipMvp.Application.Files;
using ShipMvp.Core.Security;
using ShipMvp.Core.Generated;
using ShipMvp.Core.Modules;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Abstractions;
using ShipMvp.Core.Events;
using ShipMvp.Application.Infrastructure.Analytics;

namespace ShipMvp.Application;

[Module]
[DependsOn<DatabaseModule>]
[DependsOn<AnalyticsModule>]
public class ApplicationModule : IModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register application services by convention
        services.AddServicesByConvention(typeof(ApplicationModule).Assembly);
        
        // Security services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IEncryptionService, DataProtectionEncryptionService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>(); // Use the new Infrastructure implementation

        // Repositories - Use Transient (ABP style) since UoW manages DbContext lifecycle
        // Invoice services are now registered within InvoiceModule
        services.AddTransient<IUserRepository, ShipMvp.Domain.Identity.UserRepository>();

        // Email repositories - Transient for ABP style
        // Email services are now registered within EmailMessagesModule

        // Domain services and utilities
        services.AddSingleton<IGuidGenerator, SequentialGuidGenerator>();
        // Invoice services are now registered within InvoiceModule

        // Register Shared services explicitly
        services.AddSingleton<IEventBus, LocalEventBus>();

        // Subscription repositories - Transient for ABP style
        services.AddTransient<ISubscriptionPlanRepository, ShipMvp.Domain.Subscriptions.SubscriptionPlanRepository>();
        services.AddTransient<IUserSubscriptionRepository, ShipMvp.Domain.Subscriptions.UserSubscriptionRepository>();
        services.AddTransient<ISubscriptionUsageRepository, ShipMvp.Domain.Subscriptions.SubscriptionUsageRepository>();

        // File management services - Application services should be transient
        services.AddTransient<IFileRepository, ShipMvp.Domain.Files.FileRepository>();
        services.AddScoped<IFileStorageService, GcpFileStorageService>(); // External service can be scoped
        services.AddTransient<IFileAppService, FileAppService>(); // ABP application service

        // Google Auth services are registered within GmailModule

        // Stripe services - External services can be scoped
        services.AddScoped<IStripeService, StripeService>();

        // Email services configuration - Configuration will be resolved at runtime
        services.AddOptions<ResendOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            configuration.GetSection(ResendOptions.SectionName).Bind(options);
        });

        // HTTP client for Resend service
        services.AddHttpClient<ResendEmailService>();

        // Email service registrations - Infrastructure can be scoped
        services.AddScoped<IEmailService, ResendEmailService>();
        services.AddScoped<IEmailTemplateService, DefaultEmailTemplateService>();

        // Google Analytics configuration - Configuration will be resolved at runtime
        services.AddOptions<GoogleAnalyticsOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            configuration.GetSection(GoogleAnalyticsOptions.SectionName).Bind(options);
        });

        // Memory cache for analytics
        services.AddMemoryCache();

        // Analytics service - Application service should be transient (ABP style)
        services.AddTransient<IAnalyticsService, MockAnalyticsService>();

        // Integration services
        services.AddScoped<IIntegrationManager, IntegrationManager>();
        services.AddScoped<IIntegrationRepository, ShipMvp.Domain.Integrations.IntegrationRepository>();
        services.AddScoped<IIntegrationAppService, IntegrationAppService>();


        services.AddGeneratedUnitOfWorkWrappers();
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        // Database configuration is now handled by DatabaseModule
        // Application-level configuration can be added here if needed
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ApplicationModule>>();
        logger.LogInformation("ApplicationModule configured successfully - Database handled by DatabaseModule");
    }
}

// Extension methods for service registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServicesByConvention(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract);

        foreach (var type in types)
        {
            // Register services by interface convention
            if (typeof(ITransientService).IsAssignableFrom(type))
            {
                var interfaces = type.GetInterfaces().Where(i => i != typeof(ITransientService));
                foreach (var @interface in interfaces)
                {
                    services.AddTransient(@interface, type);
                }
            }
            else if (typeof(IScopedService).IsAssignableFrom(type))
            {
                var interfaces = type.GetInterfaces().Where(i => i != typeof(IScopedService));
                foreach (var @interface in interfaces)
                {
                    services.AddScoped(@interface, type);
                }
            }
            else if (typeof(ISingletonService).IsAssignableFrom(type))
            {
                var interfaces = type.GetInterfaces().Where(i => i != typeof(ISingletonService));
                foreach (var @interface in interfaces)
                {
                    services.AddSingleton(@interface, type);
                }
            }
        }

        return services;
    }
}
