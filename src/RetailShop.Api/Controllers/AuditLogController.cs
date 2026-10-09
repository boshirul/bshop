using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/audit-log")]
[Authorize(Policy = Permissions.Administration.ViewAuditLog)]
public sealed class AuditLogController(IAuditLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<AuditLogItem>>.Success(
            await service.GetAsync(search, from, to, page, pageSize, cancellationToken)));
}
