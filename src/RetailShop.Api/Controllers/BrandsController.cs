using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Products;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/brands")]
[Authorize(Policy = Permissions.Products.View)]
public sealed class BrandsController(IProductMasterDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<NamedMasterDataItem>>>> Get(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<NamedMasterDataItem>>.Success(
            await service.GetBrandsAsync(cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<NamedMasterDataItem>>> Create(
        SaveNamedMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateBrandAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<NamedMasterDataItem>.Success(result.Value!))
            : BadRequest(ApiResponse<NamedMasterDataItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<NamedMasterDataItem>>> Update(
        Guid id,
        SaveNamedMasterDataRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateBrandAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<NamedMasterDataItem>.Success(result.Value!))
            : BadRequest(ApiResponse<NamedMasterDataItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteBrandAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
