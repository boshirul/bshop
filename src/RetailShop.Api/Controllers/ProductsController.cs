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
    [HttpGet("pos-search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PosProductSearchItem>>>> PosSearch(
        [FromQuery] string? search,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<IReadOnlyCollection<PosProductSearchItem>>.Success(
            await service.SearchForPosAsync(search, limit, cancellationToken)));

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

    [HttpGet("{id:guid}/media")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProductMediaItem>>>> GetMedia(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<ProductMediaItem>>.Success(
            await service.GetMediaAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/media")]
    [Authorize(Policy = Permissions.Products.Manage)]
    [RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ProductMediaItem>>> UploadMedia(
        Guid id,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.AddMediaAsync(id,
            new ProductMediaUpload(stream, file.FileName, file.ContentType, file.Length),
            User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<ProductMediaItem>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductMediaItem>.Failure(result.Errors));
    }

    [HttpPut("{productId:guid}/media/{mediaId:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    [RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ProductMediaItem>>> ReplaceMedia(
        Guid productId, Guid mediaId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.ReplaceMediaAsync(productId, mediaId,
            new ProductMediaUpload(stream, file.FileName, file.ContentType, file.Length),
            User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<ProductMediaItem>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductMediaItem>.Failure(result.Errors));
    }

    [HttpPut("{productId:guid}/media/{mediaId:guid}/poster")]
    [Authorize(Policy = Permissions.Products.Manage)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ProductMediaItem>>> ReplaceVideoPoster(
        Guid productId, Guid mediaId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.ReplaceVideoPosterAsync(productId, mediaId,
            new ProductMediaUpload(stream, file.FileName, file.ContentType, file.Length),
            User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<ProductMediaItem>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductMediaItem>.Failure(result.Errors));
    }

    [HttpDelete("{productId:guid}/media/{mediaId:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteMedia(
        Guid productId, Guid mediaId, CancellationToken cancellationToken)
    {
        var result = await service.DeleteMediaAsync(productId, mediaId, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }

    [HttpPost("{productId:guid}/media/{mediaId:guid}/primary")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> SetPrimaryMedia(
        Guid productId, Guid mediaId, CancellationToken cancellationToken)
    {
        var result = await service.SetPrimaryMediaAsync(productId, mediaId, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }

    [HttpPut("{productId:guid}/media/order")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProductMediaItem>>>> ReorderMedia(
        Guid productId, ReorderProductMediaRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ReorderMediaAsync(productId, request, User.GetRequiredUserId(), cancellationToken);
        return result.Succeeded ? Ok(ApiResponse<IReadOnlyCollection<ProductMediaItem>>.Success(result.Value!))
            : BadRequest(ApiResponse<IReadOnlyCollection<ProductMediaItem>>.Failure(result.Errors));
    }

    [HttpGet("{productId:guid}/media/{mediaId:guid}/content")]
    public async Task<IActionResult> MediaContent(
        Guid productId, Guid mediaId, [FromQuery] bool thumbnail, CancellationToken cancellationToken)
    {
        var result = await service.GetMediaFileAsync(productId, mediaId, thumbnail, false, cancellationToken);
        if (!result.Succeeded) return NotFound();
        return PhysicalFile(result.Value!.FilePath, result.Value.ContentType, enableRangeProcessing: true);
    }
}
