using System.Security.Claims;
using RetailShop.Application.Security;

namespace RetailShop.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException(
                "The authenticated principal does not contain a valid user identifier.");
    }

    public static bool HasPermission(
        this ClaimsPrincipal principal,
        string permission) =>
        principal.HasClaim(CustomClaimTypes.Permission, permission);
}
