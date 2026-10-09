using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Application.Reports;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = Permissions.Dashboard.View)]
public sealed class DashboardController(IReportService service) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummary>>> Summary(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<DashboardSummary>.Success(
            await service.GetDashboardAsync(cancellationToken)));
}
