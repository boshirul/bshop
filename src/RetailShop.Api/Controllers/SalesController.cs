using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Sales;
using RetailShop.Application.Security;
using RetailShop.Domain.Sales;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Policy = Permissions.Sales.View)]
public sealed class SalesController(ISaleService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SaleListItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] Guid? customerId,
        [FromQuery] SaleStatus? status,
        [FromQuery] SalePaymentStatus? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<SaleListItem>>.Success(
            await service.GetSalesAsync(
                search,
                customerId,
                status,
                paymentStatus,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SaleDetailItem>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSaleAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SaleDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<SaleDetailItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Sales.Create)]
    public async Task<ActionResult<ApiResponse<SaleDetailItem>>> Create(
        CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateSaleAsync(
            request,
            User.GetRequiredUserId(),
            User.HasPermission(Permissions.Sales.ChangePrice),
            User.HasPermission(Permissions.Sales.Discount),
            User.HasPermission(Permissions.Sales.SellOnDue),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetById),
                new { id = result.Value!.Id },
                ApiResponse<SaleDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<SaleDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = Permissions.Customers.CollectDue)]
    public async Task<ActionResult<ApiResponse<SaleDetailItem>>> Payment(
        Guid id,
        RecordSalePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordPaymentAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SaleDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SaleDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.Sales.Cancel)]
    public async Task<ActionResult<ApiResponse<SaleDetailItem>>> Cancel(
        Guid id,
        CancelSaleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelSaleAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SaleDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SaleDetailItem>.Failure(result.Errors));
    }
}
