using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Products;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = Permissions.Products.View)]
public sealed class ProductsController(IProductService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductListItem>>>> Get(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<ProductListItem>>.Success(
            await service.GetProductsAsync(
                search,
                categoryId,
                brandId,
                isActive,
                page,
                pageSize,
                cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProductDetail>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetProductAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ProductDetail>.Success(result.Value!))
            : NotFound(ApiResponse<ProductDetail>.Failure(result.Errors));
    }

    [HttpGet("by-barcode/{barcode}")]
    public async Task<ActionResult<ApiResponse<ProductDetail>>> GetByBarcode(
        string barcode,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByBarcodeAsync(barcode, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ProductDetail>.Success(result.Value!))
            : NotFound(ApiResponse<ProductDetail>.Failure(result.Errors));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<ProductDetail>>> Create(
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateProductAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetById),
                new { id = result.Value!.Id },
                ApiResponse<ProductDetail>.Success(result.Value))
            : BadRequest(ApiResponse<ProductDetail>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<ProductDetail>>> Update(
        Guid id,
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateProductAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ProductDetail>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductDetail>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteProductAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<ProductImageItem>>> AddImage(
        Guid id,
        AddProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AddImageAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ProductImageItem>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductImageItem>.Failure(result.Errors));
    }

    [HttpDelete("{productId:guid}/images/{imageId:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteImage(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteImageAsync(
            productId,
            imageId,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
