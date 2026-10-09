using RetailShop.Application.Security;

namespace RetailShop.Tests.Security;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void PermissionNames_AreUniqueAndLowercase()
    {
        var names = Permissions.All.Select(permission => permission.Name).ToArray();

        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.All(names, name => Assert.Equal(name.ToLowerInvariant(), name));
    }

    [Fact]
    public void DefaultRoles_AreUnique()
    {
        Assert.Equal(
            DefaultRoles.All.Count,
            DefaultRoles.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
