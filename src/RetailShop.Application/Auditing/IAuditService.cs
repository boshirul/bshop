namespace RetailShop.Application.Auditing;

using RetailShop.Application.Common;

public interface IAuditService
{
    Task WriteAsync(
        string action,
        string entityType,
        string? entityId,
        string? description,
        Guid? performedBy,
        CancellationToken cancellationToken = default);
}

public sealed record AuditLogItem(
    Guid Id,
    DateTimeOffset OccurredOn,
    Guid? PerformedBy,
    string Action,
    string EntityType,
    string? EntityId,
    string? Description);

public interface IAuditLogService
{
    Task<PagedResult<AuditLogItem>> GetAsync(
        string? search,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
