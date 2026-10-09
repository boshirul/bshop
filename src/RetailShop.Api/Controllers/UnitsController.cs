using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Products;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/units")]
[Authorize(Policy = Permissions.Products.View)]
public sealed class UnitsController(IProductMasterDataService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UnitItem>>>> Get(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyCollection<UnitItem>>.Success(
            await service.GetUnitsAsync(cancellationToken)));

    [HttpPost]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<UnitItem>>> Create(
        SaveUnitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateUnitAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<UnitItem>.Success(result.Value!))
            : BadRequest(ApiResponse<UnitItem>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Manage)]
    public async Task<ActionResult<ApiResponse<UnitItem>>> Update(
        Guid id,
        SaveUnitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateUnitAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<UnitItem>.Success(result.Value!))
            : BadRequest(ApiResponse<UnitItem>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Products.Delete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteUnitAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
