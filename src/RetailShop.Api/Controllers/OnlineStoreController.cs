using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RetailShop.Application.Common;
using RetailShop.Application.OnlineOrders;
using RetailShop.Application.Products;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/online-store")]
[AllowAnonymous]
[EnableRateLimiting("PublicStore")]
public sealed class OnlineStoreController(IOnlineOrderService service, IProductService productService) : ControllerBase
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

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OnlineStoreCategoryItem>>>> Categories(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<OnlineStoreCategoryItem>>.Success(
            await service.GetStoreCategoriesAsync(cancellationToken)));

    [HttpGet("policies")]
    public async Task<ActionResult<ApiResponse<OnlineStorePoliciesItem>>> Policies(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<OnlineStorePoliciesItem>.Success(
            await service.GetStorePoliciesAsync(cancellationToken)));

    [HttpGet("products/{productId:guid}/media")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OnlineProductMediaItem>>>> Media(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetStoreProductMediaAsync(productId, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<IReadOnlyCollection<OnlineProductMediaItem>>.Success(result.Value!))
            : NotFound(ApiResponse<IReadOnlyCollection<OnlineProductMediaItem>>.Failure(result.Errors));
    }

    [HttpGet("products/{productId:guid}/media/{mediaId:guid}/content")]
    public async Task<IActionResult> MediaContent(
        Guid productId,
        Guid mediaId,
        [FromQuery] bool thumbnail,
        CancellationToken cancellationToken)
    {
        var result = await productService.GetMediaFileAsync(
            productId, mediaId, thumbnail, publicOnly: true, cancellationToken);
        if (!result.Succeeded) return NotFound();

        // Revalidate eligibility on every request. This prevents browser caches from
        // retaining a file after an administrator withdraws it from the public store.
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(result.Value!.FilePath, result.Value.ContentType, enableRangeProcessing: true);
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
