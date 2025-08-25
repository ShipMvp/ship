using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipMvp.Core.Abstractions;

namespace ShipMvp.Core.Modules
{

    /// <summary>
    /// Lean module loader inspired by ABP but simpler
    /// Discovers and loads modules with dependency resolution
    /// </summary>
    public static class ModuleLoader
    {
        /// <summary>
        /// Registers modules and their dependencies
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="moduleTypes">Types of modules to register</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddModules(this IServiceCollection services, params Type[] moduleTypes)
        {
            var container = ModuleContainer.Instance;
            var sortedModules = container.GetAllModules(moduleTypes);

            var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("ModuleLoader");

            foreach (var module in sortedModules)
            {
                logger.LogInformation("Configuring services for module: {ModuleName}", module.GetType().Name);
                module.ConfigureServices(services);
            }

            // Store module instances for later configuration
            services.AddSingleton(sortedModules);

            return services;
        }

        /// <summary>
        /// Configures all registered modules in the application pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <param name="env">The hosting environment</param>
        /// <returns>The application builder for chaining</returns>
        public static IApplicationBuilder ConfigureModules(this IApplicationBuilder app, IHostEnvironment env)
        {
            var moduleInstances = app.ApplicationServices.GetRequiredService<IEnumerable<IModule>>();
            var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("ModuleLoader");

            foreach (var instance in moduleInstances)
            {
                logger.LogInformation("Configuring application pipeline for module: {ModuleName}", instance.GetType().Name);
                instance.Configure(app, env);
            }

            return app;
        }
    }
}
