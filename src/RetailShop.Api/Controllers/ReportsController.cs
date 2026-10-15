using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Reports;
using RetailShop.Application.Security;
using RetailShop.Application.Settings;
using RetailShop.Reporting;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(
    IReportService service,
    ISettingsService settingsService,
    IManagementReportExporter exporter) : ControllerBase
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

    [HttpGet("sales-items")]
    [Authorize(Policy = Permissions.Reports.Sales)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SalesItemReportItem>>>> SalesItems(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var (start, end) = DefaultRange(from, to);
        return Ok(ApiResponse<IReadOnlyCollection<SalesItemReportItem>>.Success(
            await service.GetSalesItemsAsync(start, end, cancellationToken)));
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

    [HttpGet("catalogue")]
    public ActionResult<ApiResponse<IReadOnlyCollection<ManagementReportCatalogueItem>>> Catalogue() =>
        Ok(ApiResponse<IReadOnlyCollection<ManagementReportCatalogueItem>>.Success(
            service.GetManagementReportCatalogue()));

    [HttpGet("management/{kind}")]
    public async Task<ActionResult<ApiResponse<ManagementReportPreview>>> ManagementPreview(
        ManagementReportKind kind,
        [FromQuery] ManagementReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(kind)) return Forbid();
        if (!ValidRequest(kind, request, out var error)) return BadRequest(ApiResponse<object>.Failure([error]));
        return Ok(ApiResponse<ManagementReportPreview>.Success(
            await service.GetManagementReportAsync(kind, request, cancellationToken)));
    }

    [HttpGet("management/{kind}/export")]
    public async Task<IActionResult> Export(
        ManagementReportKind kind,
        [FromQuery] string format,
        [FromQuery] ManagementReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(kind)) return Forbid();
        if (!ValidRequest(kind, request, out var error)) return BadRequest(ApiResponse<object>.Failure([error]));
        if (!string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<object>.Failure(["Export format must be pdf or xlsx."]));
        }
        var report = await service.GetManagementReportAsync(kind, request, cancellationToken);
        var shop = await settingsService.GetShopAsync(cancellationToken);
        var file = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? exporter.ExportPdf(report, shop)
            : exporter.ExportXlsx(report, shop);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private bool HasPermission(ManagementReportKind kind)
    {
        var permission = kind switch
        {
            ManagementReportKind.Profit => Permissions.Reports.Profit,
            ManagementReportKind.Inventory => Permissions.Reports.Inventory,
            ManagementReportKind.ExpenseRegister or ManagementReportKind.MonthlyExpenses or ManagementReportKind.ExpenseCategorySummary => Permissions.Expenses.Reports,
            _ => Permissions.Reports.Sales
        };
        return User.HasPermission(permission);
    }

    private static bool ValidRequest(ManagementReportKind kind, ManagementReportRequest request, out string error)
    {
        error = string.Empty;
        if (request.Month is < 1 or > 12)
        {
            error = "Month must be between 1 and 12.";
            return false;
        }
        if (request.Year is < 2000 or > 9999)
        {
            error = "Year must be a four-digit calendar year.";
            return false;
        }
        if (request.From is not null && request.To is not null && request.To < request.From)
        {
            error = "The end date cannot be before the start date.";
            return false;
        }
        if (kind == ManagementReportKind.MonthlySales && (request.Month is null || request.Year is null))
        {
            error = "Monthly sales requires a month and year.";
            return false;
        }
        if (kind == ManagementReportKind.YearlySales && request.Year is null)
        {
            error = "Yearly sales requires a year.";
            return false;
        }
        if (kind == ManagementReportKind.MonthlyExpenses && (request.Month is null || request.Year is null))
        {
            error = "Monthly expenses requires a month and year.";
            return false;
        }
        return true;
    }

    private static (DateOnly From, DateOnly To) DefaultRange(
        DateOnly? from,
        DateOnly? to)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return (from ?? today.AddDays(-30), to ?? today);
    }
}
