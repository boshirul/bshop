using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Products;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/subcategories")]
[Authorize(Policy = Permissions.Products.View)]
public sealed class SubCategoriesController(IProductMasterDataService service)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubCategoryItem>>>> Get(
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<SubCategoryItem>>.Success(
            await service.GetSubCategoriesAsync(categoryId, cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<SubCategoryItem>>> Create(
        SaveSubCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateSubCategoryAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SubCategoryItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SubCategoryItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<SubCategoryItem>>> Update(
        Guid id,
        SaveSubCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateSubCategoryAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<SubCategoryItem>.Success(result.Value!))
            : BadRequest(ApiResponse<SubCategoryItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteSubCategoryAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
