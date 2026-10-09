using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.OnlineOrders;
using RetailShop.Application.Security;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/online-orders")]
[Authorize(Policy = Permissions.OnlineOrders.View)]
public sealed class OnlineOrdersController(IOnlineOrderService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OnlineOrderListItem>>>>
        List(
            [FromQuery] string? search,
            [FromQuery] OnlineOrderStatus? status,
            [FromQuery] OnlineOrderSource? source,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<OnlineOrderListItem>>.Success(
            await service.GetOrdersAsync(
                search,
                status,
                source,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Detail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOrderAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OnlineOrderDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.OnlineOrders.Manage)]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Create(
        CreateOnlineOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateOrderAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(Detail),
                new { id = result.Value!.Id },
                ApiResponse<OnlineOrderDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Permissions.OnlineOrders.Manage)]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Confirm(
        Guid id,
        ConfirmOnlineOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ConfirmAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OnlineOrderDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/courier")]
    [Authorize(Policy = Permissions.OnlineOrders.Manage)]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Courier(
        Guid id,
        AssignCourierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AssignCourierAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OnlineOrderDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.OnlineOrders.Manage)]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Cancel(
        Guid id,
        CancelOnlineOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CancelAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OnlineOrderDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Policy = Permissions.OnlineOrders.Manage)]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Deliver(
        Guid id,
        DeliverOnlineOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.DeliverAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OnlineOrderDetailItem>.Success(result.Value!))
            : BadRequest(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }
}
