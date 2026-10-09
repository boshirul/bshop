namespace RetailShop.Infrastructure.Auditing;

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow;

    public Guid? PerformedBy { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? Description { get; set; }
}
