using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Purchases;
using RetailShop.Application.Security;
using RetailShop.Domain.Purchases;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize(Policy = Permissions.Purchases.View)]
public sealed class PurchasesController(IPurchaseService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseListItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] Guid? supplierId,
        [FromQuery] PurchaseStatus? status,
        [FromQuery] PurchasePaymentStatus? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<PurchaseListItem>>.Success(
            await service.GetPurchasesAsync(
                search,
                supplierId,
                status,
                paymentStatus,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseDetailItem>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPurchaseAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<PurchaseDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<PurchaseDetailItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Purchases.Manage)]
    public async Task<ActionResult<ApiResponse<PurchaseDetailItem>>> Create(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreatePurchaseAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetById),
                new { id = result.Value!.Id },
                ApiResponse<PurchaseDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<PurchaseDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Policy = Permissions.Purchases.PaySupplier)]
    public async Task<ActionResult<ApiResponse<PurchaseDetailItem>>> Payment(
        Guid id,
        RecordPurchasePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordPaymentAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<PurchaseDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<PurchaseDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/returns")]
    [Authorize(Policy = Permissions.Purchases.Manage)]
    public async Task<ActionResult<ApiResponse<PurchaseDetailItem>>> Return(
        Guid id,
        CreatePurchaseReturnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateReturnAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<PurchaseDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<PurchaseDetailItem>.Failure(result.Errors));
    }
}
