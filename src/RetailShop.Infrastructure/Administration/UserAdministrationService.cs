using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Administration;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Security;
using RetailShop.Infrastructure.Identity;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Administration;

internal sealed class UserAdministrationService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    RetailShopDbContext dbContext,
    IAuditService auditService) : IUserAdministrationService
{
    public async Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(
        CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .ToArrayAsync(cancellationToken);

        var results = new List<UserSummary>(users.Length);
        foreach (var user in users)
        {
            results.Add(await MapUserAsync(user));
        }

        return results;
    }

    public async Task<OperationResult<UserSummary>> GetUserAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null
            ? OperationResult<UserSummary>.Failure("User not found.")
            : OperationResult<UserSummary>.Success(await MapUserAsync(user));
    }

    public async Task<OperationResult<UserSummary>> CreateUserAsync(
        CreateUserRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return OperationResult<UserSummary>.Failure(
                "A user with this email already exists.");
        }

        var rolesResult = await ValidateRolesAsync(request.Roles, cancellationToken);
        if (!rolesResult.Succeeded)
        {
            return OperationResult<UserSummary>.Failure(rolesResult.Errors);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            EmailConfirmed = true,
            CreatedBy = performedBy
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return OperationResult<UserSummary>.Failure(
                createResult.Errors.Select(error => error.Description));
        }

        var addRolesResult = await userManager.AddToRolesAsync(user, rolesResult.Value!);
        if (!addRolesResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return OperationResult<UserSummary>.Failure(
                addRolesResult.Errors.Select(error => error.Description));
        }

        await auditService.WriteAsync(
            "Create",
            "User",
            user.Id.ToString(),
            $"Created user {user.Email}.",
            performedBy,
            cancellationToken);

        return OperationResult<UserSummary>.Success(await MapUserAsync(user));
    }

    public async Task<OperationResult<UserSummary>> UpdateUserAsync(
        Guid id,
        UpdateUserRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return OperationResult<UserSummary>.Failure("User not found.");
        }

        var rolesResult = await ValidateRolesAsync(request.Roles, cancellationToken);
        if (!rolesResult.Succeeded)
        {
            return OperationResult<UserSummary>.Failure(rolesResult.Errors);
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.IsActive = request.IsActive;
        user.LastModifiedBy = performedBy;
        user.LastModifiedOn = DateTimeOffset.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return OperationResult<UserSummary>.Failure(
                updateResult.Errors.Select(error => error.Description));
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var requestedRoles = rolesResult.Value!;
        var rolesToRemove = currentRoles.Except(requestedRoles).ToArray();
        var rolesToAdd = requestedRoles.Except(currentRoles).ToArray();

        if (rolesToRemove.Length > 0)
        {
            await userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (rolesToAdd.Length > 0)
        {
            await userManager.AddToRolesAsync(user, rolesToAdd);
        }

        await auditService.WriteAsync(
            "Update",
            "User",
            user.Id.ToString(),
            $"Updated user {user.Email}.",
            performedBy,
            cancellationToken);

        return OperationResult<UserSummary>.Success(await MapUserAsync(user));
    }

    public async Task<OperationResult<bool>> DeactivateUserAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (id == performedBy)
        {
            return OperationResult<bool>.Failure(
                "You cannot deactivate your own account.");
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return OperationResult<bool>.Failure("User not found.");
        }

        user.IsActive = false;
        user.LastModifiedBy = performedBy;
        user.LastModifiedOn = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == id && token.RevokedOn == null)
            .ToArrayAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedOn = DateTimeOffset.UtcNow;
            token.RevocationReason = "User deactivated";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Deactivate",
            "User",
            user.Id.ToString(),
            $"Deactivated user {user.Email}.",
            performedBy,
            cancellationToken);

        return OperationResult<bool>.Success(true);
    }

    public async Task<IReadOnlyCollection<RoleSummary>> GetRolesAsync(
        CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToArrayAsync(cancellationToken);

        var results = new List<RoleSummary>(roles.Length);
        foreach (var role in roles)
        {
            results.Add(await MapRoleAsync(role, cancellationToken));
        }

        return results;
    }

    public async Task<OperationResult<RoleSummary>> CreateRoleAsync(
        CreateRoleRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await roleManager.RoleExistsAsync(name))
        {
            return OperationResult<RoleSummary>.Failure("This role already exists.");
        }

        var role = new ApplicationRole(name);
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return OperationResult<RoleSummary>.Failure(
                result.Errors.Select(error => error.Description));
        }

        await auditService.WriteAsync(
            "Create",
            "Role",
            role.Id.ToString(),
            $"Created role {name}.",
            performedBy,
            cancellationToken);

        return OperationResult<RoleSummary>.Success(
            await MapRoleAsync(role, cancellationToken));
    }

    public async Task<OperationResult<RoleSummary>> UpdateRoleAsync(
        Guid id,
        UpdateRoleRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null)
        {
            return OperationResult<RoleSummary>.Failure("Role not found.");
        }

        if (DefaultRoles.All.Contains(role.Name ?? string.Empty))
        {
            return OperationResult<RoleSummary>.Failure(
                "Default roles cannot be renamed.");
        }

        role.Name = request.Name.Trim();
        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            return OperationResult<RoleSummary>.Failure(
                result.Errors.Select(error => error.Description));
        }

        await auditService.WriteAsync(
            "Update",
            "Role",
            role.Id.ToString(),
            $"Renamed role to {role.Name}.",
            performedBy,
            cancellationToken);

        return OperationResult<RoleSummary>.Success(
            await MapRoleAsync(role, cancellationToken));
    }

    public async Task<IReadOnlyCollection<PermissionSummary>> GetPermissionsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Group)
            .ThenBy(permission => permission.DisplayName)
            .Select(permission => new PermissionSummary(
                permission.Id,
                permission.Name,
                permission.DisplayName,
                permission.Group))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<RoleSummary>> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateRolePermissionsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null)
        {
            return OperationResult<RoleSummary>.Failure("Role not found.");
        }

        var requestedNames = request.Permissions.Distinct().ToArray();
        var permissions = await dbContext.Permissions
            .Where(permission => requestedNames.Contains(permission.Name))
            .ToArrayAsync(cancellationToken);

        if (permissions.Length != requestedNames.Length)
        {
            return OperationResult<RoleSummary>.Failure(
                "One or more permissions are invalid.");
        }

        var existing = await dbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .ToArrayAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existing);
        dbContext.RolePermissions.AddRange(permissions.Select(permission =>
            new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            }));
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            "PermissionsChanged",
            "Role",
            role.Id.ToString(),
            $"Updated permissions for role {role.Name}.",
            performedBy,
            cancellationToken);

        return OperationResult<RoleSummary>.Success(
            await MapRoleAsync(role, cancellationToken));
    }

    private async Task<UserSummary> MapUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserSummary(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.PhoneNumber,
            user.IsActive,
            roles.ToArray());
    }

    private async Task<RoleSummary> MapRoleAsync(
        ApplicationRole role,
        CancellationToken cancellationToken)
    {
        var permissions = await (
            from rolePermission in dbContext.RolePermissions
            join permission in dbContext.Permissions
                on rolePermission.PermissionId equals permission.Id
            where rolePermission.RoleId == role.Id
            orderby permission.Name
            select permission.Name)
            .ToArrayAsync(cancellationToken);

        return new RoleSummary(
            role.Id,
            role.Name ?? string.Empty,
            permissions);
    }

    private async Task<OperationResult<IReadOnlyCollection<string>>> ValidateRolesAsync(
        IReadOnlyCollection<string> requestedRoles,
        CancellationToken cancellationToken)
    {
        var names = requestedRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedNames = names
            .Select(name => roleManager.NormalizeKey(name))
            .ToArray();

        var existingRoles = await roleManager.Roles
            .Where(role =>
                role.NormalizedName != null &&
                normalizedNames.Contains(role.NormalizedName))
            .Select(role => role.Name!)
            .ToArrayAsync(cancellationToken);

        return existingRoles.Length == names.Length
            ? OperationResult<IReadOnlyCollection<string>>.Success(existingRoles)
            : OperationResult<IReadOnlyCollection<string>>.Failure(
                "One or more roles are invalid.");
    }
}
