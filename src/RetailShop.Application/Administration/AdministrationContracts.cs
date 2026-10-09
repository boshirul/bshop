using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;

namespace RetailShop.Application.Administration;

public sealed record UserSummary(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsActive,
    IReadOnlyCollection<string> Roles);

public sealed record CreateUserRequest(
    [param: Required] string FullName,
    [param: Required, EmailAddress] string Email,
    string? PhoneNumber,
    [param: Required, MinLength(8)] string Password,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateUserRequest(
    [param: Required] string FullName,
    string? PhoneNumber,
    bool IsActive,
    IReadOnlyCollection<string> Roles);

public sealed record RoleSummary(
    Guid Id,
    string Name,
    IReadOnlyCollection<string> Permissions);

public sealed record CreateRoleRequest(
    [param: Required] string Name);

public sealed record UpdateRoleRequest(
    [param: Required] string Name);

public sealed record UpdateRolePermissionsRequest(
    IReadOnlyCollection<string> Permissions);

public sealed record PermissionSummary(
    Guid Id,
    string Name,
    string DisplayName,
    string Group);

public interface IUserAdministrationService
{
    Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken cancellationToken);

    Task<OperationResult<UserSummary>> GetUserAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<UserSummary>> CreateUserAsync(
        CreateUserRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<UserSummary>> UpdateUserAsync(
        Guid id,
        UpdateUserRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<bool>> DeactivateUserAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(CancellationToken cancellationToken);

    Task<OperationResult<RoleSummary>> CreateRoleAsync(
        CreateRoleRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<RoleSummary>> UpdateRoleAsync(
        Guid id,
        UpdateRoleRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PermissionSummary>> GetPermissionsAsync(
        CancellationToken cancellationToken);

    Task<OperationResult<RoleSummary>> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
}
