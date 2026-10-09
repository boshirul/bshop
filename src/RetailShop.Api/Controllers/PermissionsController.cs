using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Application.Administration;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize(Policy = Permissions.Administration.ManageRoles)]
public sealed class PermissionsController(IUserAdministrationService administrationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PermissionSummary>>>> Get(
        CancellationToken cancellationToken)
    {
        var permissions =
            await administrationService.GetPermissionsAsync(cancellationToken);
        return Ok(
            ApiResponse<IReadOnlyCollection<PermissionSummary>>.Success(permissions));
    }
}
