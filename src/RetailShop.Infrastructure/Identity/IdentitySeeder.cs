using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RetailShop.Application.Security;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Identity;

internal sealed class IdentitySeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    RetailShopDbContext dbContext,
    IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync();
        await SeedRolePermissionsAsync(cancellationToken);
        await SeedAdministratorAsync();
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.Permissions.ToDictionaryAsync(
            permission => permission.Name,
            cancellationToken);

        foreach (var definition in Permissions.All)
        {
            if (existing.TryGetValue(definition.Name, out var permission))
            {
                permission.DisplayName = definition.DisplayName;
                permission.Group = definition.Group;
                continue;
            }

            dbContext.Permissions.Add(new PermissionRecord
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                Group = definition.Group
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in DefaultRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
                EnsureSucceeded(result, $"create role {roleName}");
            }
        }
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var permissionIds = await dbContext.Permissions.ToDictionaryAsync(
            permission => permission.Name,
            permission => permission.Id,
            cancellationToken);

        var mappings = new Dictionary<string, IReadOnlyCollection<string>>
        {
            [DefaultRoles.Admin] = Permissions.All.Select(permission => permission.Name).ToArray(),
            [DefaultRoles.Manager] = Permissions.All
                .Select(permission => permission.Name)
                .Except(
                [
                    Permissions.Products.Delete,
                    Permissions.Administration.ManageUsers,
                    Permissions.Administration.ManageRoles
                ])
                .ToArray(),
            [DefaultRoles.Salesperson] =
            [
                Permissions.Dashboard.View,
                Permissions.Products.View,
                Permissions.Inventory.View,
                Permissions.Sales.View,
                Permissions.Sales.Create,
                Permissions.Sales.ChangePrice,
                Permissions.Sales.Discount,
                Permissions.Sales.SellOnDue,
                Permissions.Quotations.View,
                Permissions.Quotations.Manage,
                Permissions.Quotations.Convert,
                Permissions.Customers.View,
                Permissions.Customers.Manage,
                Permissions.Customers.CollectDue,
                Permissions.Customers.ViewAccounts,
                Permissions.Customers.PrintStatement,
                Permissions.Suppliers.View,
                Permissions.Returns.Request,
                Permissions.Warranty.View,
                Permissions.Warranty.CreateClaim,
                Permissions.Reports.Sales
            ]
        };

        foreach (var (roleName, permissionNames) in mappings)
        {
            var role = await roleManager.FindByNameAsync(roleName)
                ?? throw new InvalidOperationException($"Seed role '{roleName}' was not found.");
            var existingPermissionIds = await dbContext.RolePermissions
                .Where(rolePermission => rolePermission.RoleId == role.Id)
                .Select(rolePermission => rolePermission.PermissionId)
                .ToArrayAsync(cancellationToken);

            foreach (var permissionName in permissionNames)
            {
                var permissionId = permissionIds[permissionName];
                if (!existingPermissionIds.Contains(permissionId))
                {
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionId
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdministratorAsync()
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        var fullName = configuration["Seed:AdminFullName"] ?? "System Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult, "create the seed administrator");
        }

        if (!await userManager.IsInRoleAsync(user, DefaultRoles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(user, DefaultRoles.Admin);
            EnsureSucceeded(roleResult, "assign the Admin role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Unable to {action}: {errors}");
    }
}
