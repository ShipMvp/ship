using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
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
        });

        // Use the standardized EF persistence registration
        services.AddEfPersistence<TDbContext>();

    // Allow consumers requesting the base AppDbContext to resolve to the concrete TDbContext
    // services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<TDbContext>());
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

            // Seed initial data asynchronously in the background to avoid blocking startup
            Console.WriteLine("[DEBUG] Starting background seeding task...");
            // _ = Task.Run(async () =>
            // {
            //     try
            //     {
            //         Console.WriteLine("[DEBUG] Background seeding: Creating scope...");
            //         using var seedScope = app.ApplicationServices.CreateScope();
            //         var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            //         Console.WriteLine("[DEBUG] Background seeding: Starting DataSeeder...");
            //         await DataSeeder.SeedAsync(seedContext, seedScope.ServiceProvider);
            //         Console.WriteLine("[DEBUG] Background seeding: Completed successfully.");
            //     }
            //     catch (Exception ex)
            //     {
            //         var logger = app.ApplicationServices.GetService<ILogger<DatabaseModule>>();
            //         logger?.LogError(ex, "Error occurred during data seeding");
            //         Console.WriteLine($"[DEBUG] Background seeding error: {ex.Message}");
            //     }
            // });

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