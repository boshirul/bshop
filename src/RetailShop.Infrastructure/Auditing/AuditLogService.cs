using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Auditing;

internal sealed class AuditLogService(RetailShopDbContext dbContext)
    : IAuditLogService
{
    public async Task<PagedResult<AuditLogItem>> GetAsync(
        string? search,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.AuditLogs.AsNoTracking();

        if (from.HasValue)
        {
            query = query.Where(log => log.OccurredOn >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.OccurredOn <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(log =>
                EF.Functions.ILike(log.Action, $"%{term}%") ||
                EF.Functions.ILike(log.EntityType, $"%{term}%") ||
                (log.EntityId != null && EF.Functions.ILike(log.EntityId, $"%{term}%")) ||
                (log.Description != null && EF.Functions.ILike(log.Description, $"%{term}%")));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.OccurredOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditLogItem(
                log.Id,
                log.OccurredOn,
                log.PerformedBy,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Description))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<AuditLogItem>(items, page, pageSize, total);
    }
}
