using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Inventory;
using RetailShop.Application.Security;
using RetailShop.Domain.Inventory;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/stocks")]
[Authorize(Policy = Permissions.Inventory.View)]
public sealed class StocksController(IInventoryService service) : ControllerBase
{
    [HttpPost("opening")]
    [Authorize(Policy = Permissions.Inventory.OpeningStock)]
    public async Task<ActionResult<ApiResponse<OpeningStockResult>>> Opening(
        OpeningStockRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordOpeningStockAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OpeningStockResult>.Success(result.Value!))
            : BadRequest(ApiResponse<OpeningStockResult>.Failure(result.Errors));
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<PagedResult<CurrentStockItem>>>> Current(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CurrentStockItem>>.Success(
            await service.GetCurrentStockAsync(
                search,
                false,
                false,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResponse<PagedResult<CurrentStockItem>>>> LowStock(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CurrentStockItem>>.Success(
            await service.GetCurrentStockAsync(
                search,
                true,
                false,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("damaged")]
    public async Task<ActionResult<ApiResponse<PagedResult<CurrentStockItem>>>> Damaged(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CurrentStockItem>>.Success(
            await service.GetCurrentStockAsync(
                search,
                false,
                true,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("ledger")]
    public async Task<ActionResult<ApiResponse<PagedResult<StockLedgerItem>>>> Ledger(
        [FromQuery] Guid? productId,
        [FromQuery] StockTransactionType? transactionType,
        [FromQuery] StockBucket? bucket,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<StockLedgerItem>>.Success(
            await service.GetLedgerAsync(
                productId,
                transactionType,
                bucket,
                from,
                to,
                page,
                pageSize,
                cancellationToken)));

    [HttpPost("adjustments")]
    [Authorize(Policy = Permissions.Inventory.RequestAdjustment)]
    public async Task<ActionResult<ApiResponse<StockAdjustmentDetailItem>>> CreateAdjustment(
        CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAdjustmentAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetAdjustment),
                new { id = result.Value!.Id },
                ApiResponse<StockAdjustmentDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<StockAdjustmentDetailItem>.Failure(result.Errors));
    }

    [HttpGet("adjustments")]
    public async Task<
        ActionResult<ApiResponse<PagedResult<StockAdjustmentDetailItem>>>> Adjustments(
        [FromQuery] StockAdjustmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<StockAdjustmentDetailItem>>.Success(
            await service.GetAdjustmentsAsync(
                status,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("adjustments/{id:guid}")]
    public async Task<ActionResult<ApiResponse<StockAdjustmentDetailItem>>> GetAdjustment(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAdjustmentAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<StockAdjustmentDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<StockAdjustmentDetailItem>.Failure(result.Errors));
    }

    [HttpPost("adjustments/{id:guid}/approve")]
    [Authorize(Policy = Permissions.Inventory.ApproveAdjustment)]
    public async Task<ActionResult<ApiResponse<StockAdjustmentDetailItem>>> Approve(
        Guid id,
        ReviewStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ApproveAdjustmentAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<StockAdjustmentDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<StockAdjustmentDetailItem>.Failure(result.Errors));
    }

    [HttpPost("adjustments/{id:guid}/reject")]
    [Authorize(Policy = Permissions.Inventory.ApproveAdjustment)]
    public async Task<ActionResult<ApiResponse<StockAdjustmentDetailItem>>> Reject(
        Guid id,
        RejectStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectAdjustmentAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<StockAdjustmentDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<StockAdjustmentDetailItem>.Failure(result.Errors));
    }

    [HttpPost("adjustments/{id:guid}/reverse")]
    [Authorize(Policy = Permissions.Inventory.ApproveAdjustment)]
    public async Task<ActionResult<ApiResponse<StockAdjustmentDetailItem>>> Reverse(
        Guid id,
        ReverseStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ReverseAdjustmentAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<StockAdjustmentDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<StockAdjustmentDetailItem>.Failure(result.Errors));
    }
}
