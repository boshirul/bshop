namespace RetailShop.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public Guid? LastModifiedBy { get; set; }

    public DateTimeOffset? LastModifiedOn { get; set; }

    public bool IsDeleted { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTimeOffset? DeletedOn { get; set; }
}
