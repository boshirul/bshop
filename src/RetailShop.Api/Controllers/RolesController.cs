using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Administration;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = Permissions.Administration.ManageRoles)]
public sealed class RolesController(IUserAdministrationService administrationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RoleSummary>>>> GetRoles(
        CancellationToken cancellationToken)
    {
        var roles = await administrationService.GetRolesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyCollection<RoleSummary>>.Success(roles));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleSummary>>> CreateRole(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.CreateRoleAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<RoleSummary>.Success(result.Value!))
            : BadRequest(ApiResponse<RoleSummary>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoleSummary>>> UpdateRole(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.UpdateRoleAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<RoleSummary>.Success(result.Value!))
            : BadRequest(ApiResponse<RoleSummary>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult<ApiResponse<RoleSummary>>> UpdatePermissions(
        Guid id,
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.UpdateRolePermissionsAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<RoleSummary>.Success(result.Value!))
            : BadRequest(ApiResponse<RoleSummary>.Failure(result.Errors));
    }
}
