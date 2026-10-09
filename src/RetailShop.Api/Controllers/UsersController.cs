using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Administration;
using RetailShop.Application.Security;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Permissions.Administration.ManageUsers)]
public sealed class UsersController(IUserAdministrationService administrationService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserSummary>>>> GetUsers(
        CancellationToken cancellationToken)
    {
        var users = await administrationService.GetUsersAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyCollection<UserSummary>>.Success(users));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserSummary>>> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.GetUserAsync(id, cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<UserSummary>.Success(result.Value!))
            : NotFound(ApiResponse<UserSummary>.Failure(result.Errors));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserSummary>>> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.CreateUserAsync(
            request,
            User.GetRequiredUserId(),
            cancellationToken);

        return result.Succeeded
            ? CreatedAtAction(
                nameof(GetUser),
                new { id = result.Value!.Id },
                ApiResponse<UserSummary>.Success(result.Value))
            : BadRequest(ApiResponse<UserSummary>.Failure(result.Errors));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserSummary>>> UpdateUser(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.UpdateUserAsync(
            id,
            request,
            User.GetRequiredUserId(),
            cancellationToken);

        return result.Succeeded
            ? Ok(ApiResponse<UserSummary>.Success(result.Value!))
            : BadRequest(ApiResponse<UserSummary>.Failure(result.Errors));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeactivateUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await administrationService.DeactivateUserAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);

        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }
}
