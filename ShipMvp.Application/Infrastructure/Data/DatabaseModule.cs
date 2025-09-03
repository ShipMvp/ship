using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
using ShipMvp.Core.Persistence;
using ShipMvp.Core.Persistence.Ef;
using System.Threading.Tasks;

namespace ShipMvp.Application.Infrastructure.Data;

public abstract class DatabaseModule<TDbContext> : IModule
    where TDbContext : AppDbContext
{

    public virtual void ConfigureServices(IServiceCollection services)
    {

        // Database - Use PostgreSQL for all environments
        services.AddDbContext<TDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

            Console.WriteLine($"[DEBUG] Environment Name: {environment.EnvironmentName}");
            Console.WriteLine($"[DEBUG] Using PostgreSQL database for all environments");

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Create a new data source with JSON support for this connection
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            var dataSource = dataSourceBuilder.Build();

            // Configure EF to use the constructed NpgsqlDataSource so the DbContext has a provider
            options.UseNpgsql(dataSource);

            // Add concurrency stamp interceptor for automatic concurrency management
            options.AddInterceptors(new ConcurrencyStampInterceptor(serviceProvider));
        });

        // Use the standardized EF persistence registration
        services.AddEfPersistence<TDbContext>();

        // Allow consumers requesting the base AppDbContext to resolve to the concrete TDbContext
        // This is required so other libraries (for example OpenIddict) which call
        // UseDbContext<AppDbContext>() can resolve the registered concrete DbContext.
        services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<TDbContext>());
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        Console.WriteLine("[DEBUG] DatabaseModule.Configure starting...");

        try
        {
            // Apply database migrations
            Console.WriteLine("[DEBUG] Creating service scope...");
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TDbContext>();

            Console.WriteLine("[DEBUG] Applying database migrations...");
            context.Database.EnsureCreated();
            Console.WriteLine("[DEBUG] Database migrations applied successfully.");

            // Seed initial data synchronously at startup to ensure required data (OpenIddict clients, users, plans)
            Console.WriteLine("[DEBUG] Running DataSeeder at startup...");
            try
            {
                using var seedScope = app.ApplicationServices.CreateScope();
                var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Run synchronously so token endpoints can find seeded OpenIddict clients immediately
                DataSeeder.SeedAsync(seedContext, seedScope.ServiceProvider).GetAwaiter().GetResult();
                Console.WriteLine("[DEBUG] DataSeeder completed successfully.");
            }
            catch (Exception ex)
            {
                var logger = app.ApplicationServices.GetService<ILogger<DatabaseModule<TDbContext>>>();
                logger?.LogError(ex, "Error occurred during data seeding");
                Console.WriteLine($"[DEBUG] DataSeeder error: {ex.Message}");
            }

            Console.WriteLine("[DEBUG] DatabaseModule.Configure completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] DatabaseModule.Configure error: {ex.Message}");
            Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}