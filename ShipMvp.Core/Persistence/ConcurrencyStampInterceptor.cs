using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShipMvp.Core.Abstractions;
using ShipMvp.Core.Entities;

namespace ShipMvp.Core.Persistence;

/// <summary>
/// EF Core interceptor that automatically manages ConcurrencyStamp for entities implementing IHasConcurrencyStamp.
/// </summary>
public class ConcurrencyStampInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public ConcurrencyStampInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateConcurrencyStamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateConcurrencyStamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateConcurrencyStamps(DbContext? context)
    {
        if (context == null) return;

        var guidGenerator = _serviceProvider.GetRequiredService<IGuidGenerator>();
        var logger = _serviceProvider.GetService<ILogger<ConcurrencyStampInterceptor>>();

        var stampEntries = context.ChangeTracker.Entries<IHasConcurrencyStamp>().ToList();
        logger?.LogDebug("Processing {EntityCount} entities with concurrency stamps", stampEntries.Count);

        foreach (var entry in stampEntries)
        {
            var entityType = entry.Entity.GetType().Name;
            var entityId = GetEntityId(entry.Entity);

            switch (entry.State)
            {
                case EntityState.Added:
                    // Set initial concurrency stamp for new entities
                    if (string.IsNullOrEmpty(entry.Entity.ConcurrencyStamp))
                    {
                        var newStamp = guidGenerator.Create().ToString("N");
                        entry.Entity.ConcurrencyStamp = newStamp;
                        logger?.LogDebug("Set initial ConcurrencyStamp for new {EntityType} {EntityId}: {ConcurrencyStamp}",
                            entityType, entityId, newStamp);
                    }
                    else
                    {
                        logger?.LogDebug("New {EntityType} {EntityId} already has ConcurrencyStamp: {ConcurrencyStamp}",
                            entityType, entityId, entry.Entity.ConcurrencyStamp);
                    }
                    break;

                case EntityState.Modified:
                    // Update concurrency stamp for modified entities
                    var originalStamp = entry.OriginalValues.GetValue<string>(nameof(IHasConcurrencyStamp.ConcurrencyStamp));
                    var currentStamp = entry.Entity.ConcurrencyStamp;

                    logger?.LogDebug("Modified {EntityType} {EntityId} - Original: {OriginalStamp}, Current: {CurrentStamp}",
                        entityType, entityId, originalStamp, currentStamp);

                    if (currentStamp == originalStamp)
                    {
                        var newStamp = guidGenerator.Create().ToString("N");
                        entry.Entity.ConcurrencyStamp = newStamp;
                        logger?.LogDebug("Updated ConcurrencyStamp for modified {EntityType} {EntityId}: {OriginalStamp} -> {NewStamp}",
                            entityType, entityId, originalStamp, newStamp);
                    }
                    else
                    {
                        logger?.LogDebug("ConcurrencyStamp already updated for {EntityType} {EntityId}: {CurrentStamp}",
                            entityType, entityId, currentStamp);
                    }
                    break;

                default:

                    break;
            }
        }
    }

    private static string GetEntityId(object entity)
    {
        // Try to get Id property via reflection for logging purposes
        var idProperty = entity.GetType().GetProperty("Id");
        return idProperty?.GetValue(entity)?.ToString() ?? "Unknown";
    }
}
