using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
using ShipMvp.Integration.SemanticKernel.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ShipMvp.Integration.SemanticKernel
{
    [Module]
    public class SemanticKernelModule : IModule
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // Register core Semantic Kernel service
            services.AddScoped<ISemanticKernelService, SemanticKernelService>();
            
            // Register ActivitySource for LLM logging (as singleton since it's thread-safe)
            services.AddSingleton(sp => new ActivitySource("ShipMvp.LlmLogging"));
            
            // Configure OpenTelemetry for LLM logging
            ConfigureOpenTelemetry(services);
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            // Log OpenTelemetry configuration status
            var logger = app.ApplicationServices.GetService<ILogger<SemanticKernelModule>>();
            if (env.IsDevelopment())
            {
                logger?.LogInformation("SemanticKernel: OpenTelemetry configured for development environment with console exporter");
            }
            else
            {
                logger?.LogInformation("SemanticKernel: OpenTelemetry configured for production environment");
            }
        }
        
        private static void ConfigureOpenTelemetry(IServiceCollection services)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService("ShipMvp.SemanticKernel", "1.0.0")
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["service.component"] = "semantic-kernel",
                        ["service.instance.id"] = Environment.MachineName
                    }))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource("Microsoft.SemanticKernel")
                        .AddSource("ShipMvp.LlmLogging"); // Our custom LLM logging source

                    // Add our custom LLM telemetry processor
                    AddLlmTelemetryProcessor(tracing);

                    // Configure environment-specific settings
                    ConfigureTracingForEnvironment(services, tracing);
                });
        }

        private static void AddLlmTelemetryProcessor(TracerProviderBuilder tracing)
        {
            try
            {
                tracing.AddProcessor(sp => new LlmTelemetryProcessor(sp));
            }
            catch (Exception)
            {
                // If processor creation fails, continue without it
                // The basic OpenTelemetry tracing will still work
            }
        }

        private static void ConfigureTracingForEnvironment(IServiceCollection services, TracerProviderBuilder tracing)
        {
            var serviceProvider = services.BuildServiceProvider();
            var environment = serviceProvider.GetService<IHostEnvironment>();

            if (environment?.IsDevelopment() == true)
            {
                ConfigureDevelopmentTracing(services, tracing);
            }
            else
            {
                ConfigureProductionTracing(tracing);
            }
        }

        private static void ConfigureDevelopmentTracing(IServiceCollection services, TracerProviderBuilder tracing)
        {
            tracing
                .SetSampler(new AlwaysOnSampler()) // Always sample in development
                .AddConsoleExporter(); // Export to console for development debugging

            // Add debug logging
            services.Configure<LoggerFilterOptions>(options =>
            {
                options.AddFilter("OpenTelemetry", LogLevel.Debug);
                options.AddFilter("ShipMvp.Integration.SemanticKernel", LogLevel.Debug);
            });
        }

        private static void ConfigureProductionTracing(TracerProviderBuilder tracing)
        {
            tracing.SetSampler(new TraceIdRatioBasedSampler(0.01)); // Sample 1% without external export
        }
    }
}
