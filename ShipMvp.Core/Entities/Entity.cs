namespace ShipMvp.Core.Entities;

public abstract class Entity<TId> : IEntity<TId>, IHasConcurrencyStamp
{
    public TId Id { get; protected set; }

    // Auditing
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    // Optimistic concurrency control (ABP pattern)
    public string ConcurrencyStamp { get; set; } = string.Empty;

    protected Entity(TId id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }

}
