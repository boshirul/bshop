using RetailShop.Application.Auditing;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Auditing;

internal sealed class AuditService(RetailShopDbContext dbContext) : IAuditService
{
    public async Task WriteAsync(
        string action,
        string entityType,
        string? entityId,
        string? description,
        Guid? performedBy,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            PerformedBy = performedBy
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
