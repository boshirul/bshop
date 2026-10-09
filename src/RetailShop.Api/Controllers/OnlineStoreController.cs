using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RetailShop.Application.Common;
using RetailShop.Application.OnlineOrders;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/online-store")]
[AllowAnonymous]
[EnableRateLimiting("PublicStore")]
public sealed class OnlineStoreController(IOnlineOrderService service) : ControllerBase
{
    [HttpGet("products")]
    public async Task<ActionResult<ApiResponse<PagedResult<OnlineProductListItem>>>>
        Products(
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<OnlineProductListItem>>.Success(
            await service.GetStoreProductsAsync(
                search,
                categoryId,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("products/{id:guid}")]
    public async Task<ActionResult<ApiResponse<OnlineProductDetailItem>>> Product(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetStoreProductAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<OnlineProductDetailItem>.Success(result.Value!))
            : NotFound(ApiResponse<OnlineProductDetailItem>.Failure(result.Errors));
    }

    [HttpPost("checkout")]
    [EnableRateLimiting("Checkout")]
    public async Task<ActionResult<ApiResponse<OnlineOrderDetailItem>>> Checkout(
        CreateOnlineOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateOrderAsync(request, null, cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(OnlineOrdersController.Detail),
                "OnlineOrders",
                new { id = result.Value!.Id },
                ApiResponse<OnlineOrderDetailItem>.Success(result.Value))
            : BadRequest(ApiResponse<OnlineOrderDetailItem>.Failure(result.Errors));
    }
}
