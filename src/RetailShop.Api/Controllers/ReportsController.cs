using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Application.Reports;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(IReportService service) : ControllerBase
{
    [HttpGet("sales")]
    [Authorize(Policy = Permissions.Reports.Sales)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SalesReportItem>>>>
        Sales(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
    {
        var (start, end) = DefaultRange(from, to);
        return Ok(ApiResponse<IReadOnlyCollection<SalesReportItem>>.Success(
            await service.GetSalesAsync(start, end, cancellationToken)));
    }

    [HttpGet("profit")]
    [Authorize(Policy = Permissions.Reports.Profit)]
    public async Task<ActionResult<ApiResponse<ProfitSummary>>> Profit(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = DefaultRange(from, to);
        return Ok(ApiResponse<ProfitSummary>.Success(
            await service.GetProfitAsync(start, end, cancellationToken)));
    }

    [HttpGet("inventory")]
    [Authorize(Policy = Permissions.Reports.Inventory)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<InventoryReportItem>>>>
        Inventory(
            [FromQuery] string? search,
            CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<InventoryReportItem>>.Success(
            await service.GetInventoryAsync(search, cancellationToken)));

    [HttpGet("customer-dues")]
    [Authorize(Policy = Permissions.Reports.Sales)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CustomerDueReportItem>>>>
        CustomerDues(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<CustomerDueReportItem>>.Success(
            await service.GetCustomerDuesAsync(cancellationToken)));

    [HttpGet("operations")]
    [Authorize(Policy = Permissions.Reports.Sales)]
    public async Task<ActionResult<ApiResponse<OperationalReport>>> Operations(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<OperationalReport>.Success(
            await service.GetOperationsAsync(cancellationToken)));

    private static (DateOnly From, DateOnly To) DefaultRange(
        DateOnly? from,
        DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (from ?? today.AddDays(-30), to ?? today);
    }
}
