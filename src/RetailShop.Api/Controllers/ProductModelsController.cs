using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Products;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/product-models")]
[Authorize(Policy = Permissions.Products.View)]
public sealed class ProductModelsController(IProductMasterDataService service)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ProductModelItem>>>> Get(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<ProductModelItem>>.Success(
            await service.GetProductModelsAsync(brandId, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<ProductModelItem>>> Create(
        SaveProductModelRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateProductModelAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ProductModelItem>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductModelItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<ProductModelItem>>> Update(
        Guid id,
        SaveProductModelRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateProductModelAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<ProductModelItem>.Success(result.Value!))
            : BadRequest(ApiResponse<ProductModelItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteProductModelAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
