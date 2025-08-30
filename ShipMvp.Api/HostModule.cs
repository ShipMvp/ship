using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipMvp.Api.Auth;
using ShipMvp.Application;
using ShipMvp.Core;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
using ShipMvp.Domain.Shared.Constants;
using ShipMvp.Integration.SemanticKernel;
using System.Text.RegularExpressions;
using System.Reflection;
using ShipMvp.Application.Infrastructure.EventBus;

namespace ShipMvp.Api;

[Module]
[DependsOn<ApiModule>]
[DependsOn<AuthorizationModule>]
[DependsOn<OpenIddictModule>]
[DependsOn<SemanticKernelModule>]
public class HostModule : IModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Enhanced CORS configuration that reads from both appsettings and environment variables
        services.AddCors();

        // Configure logging
        ConfigureLogging(services);

        // Other service configurations
        services.AddDataProtection();

        // Resolve IConfiguration from the service provider instead of using a top-level 'builder' variable
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        services.AddDistributedEventBus(configuration);
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<HostModule>>();
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();

        if (env.IsDevelopment())
        {
            logger.LogInformation("Host: Development environment detected. Enhanced logging enabled.");
        }

        // Configure routing first (required before UseEndpoints)
        app.UseRouting();

        // Configure CORS after routing so CORS can use endpoint metadata if needed
        app.UseCors(builder =>
        {
            var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var logger = app.ApplicationServices.GetRequiredService<ILogger<HostModule>>();

            // Get CORS origins from configuration
            var corsOrigins = configuration["App:CorsOrigins"];
            var allowedOrigins = new List<string>();

            if (!string.IsNullOrWhiteSpace(corsOrigins))
            {
                allowedOrigins = corsOrigins
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Distinct()
                    .ToList();
            }

            // Add default development origins if none configured
            if (!allowedOrigins.Any())
            {
                allowedOrigins = new List<string>
                {
                    "http://localhost:8080"
                };
            }

            logger.LogInformation("CORS: Configuring allowed origins: {Origins}", string.Join(", ", allowedOrigins));

            builder
                .WithOrigins(allowedOrigins.ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders("Content-Disposition", "Content-Length", "X-Total-Count")
                .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
        });

        // Apply authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Configure endpoints (this must come after UseRouting)
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();

            // Note: Root path (/) and health endpoint (/health) are handled by HomeController with [AllowAnonymous]
            // Additional anonymous endpoints can be added here if needed
        });
    }

    private void ConfigureLogging(IServiceCollection services)
    {
        services.AddLogging(loggingBuilder =>
        {
            var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

            loggingBuilder.ClearProviders();
            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
            loggingBuilder.AddConfiguration(configuration.GetSection("Logging"));
        });
    }
}
