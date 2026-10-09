namespace RetailShop.Infrastructure.Identity;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public ApplicationRole Role { get; set; } = null!;

    public Guid PermissionId { get; set; }

    public PermissionRecord Permission { get; set; } = null!;
}
