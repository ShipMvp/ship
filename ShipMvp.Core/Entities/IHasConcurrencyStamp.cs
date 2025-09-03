namespace ShipMvp.Core.Entities;

/// <summary>
/// Interface for entities that support optimistic concurrency control.
/// Based on ABP framework's IHasConcurrencyStamp pattern.
/// </summary>
public interface IHasConcurrencyStamp
{
    /// <summary>
    /// Concurrency stamp used for optimistic concurrency control.
    /// This should be automatically managed by the framework.
    /// </summary>
    string ConcurrencyStamp { get; set; }
}
