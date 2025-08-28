using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ShipMvp.Application.Infrastructure.Data;

public abstract class BaseAppDbContextFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext> where TDbContext : AppDbContext
{
    public abstract TDbContext CreateDbContext(string[] args);
            

    public DbContextOptions<TDbContext> GetOptions()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        
        // Build configuration to read from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
        
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection string not found in configuration.");
        }
        
        // Use PostgreSQL for migrations design-time
        optionsBuilder.UseNpgsql(connectionString);

        var options = optionsBuilder.Options;
        return options;
    }
}
