using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RetailShop.Application.Authentication;
using RetailShop.Application.Security;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Identity;

internal sealed class TokenService(
    UserManager<ApplicationUser> userManager,
    RetailShopDbContext dbContext,
    IOptions<JwtOptions> jwtOptions)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AccessTokenResult> CreateAccessTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var normalizedRoles = roles
            .Select(role => role.ToUpperInvariant())
            .ToArray();

        var permissions = await (
            from role in dbContext.Roles
            join rolePermission in dbContext.RolePermissions on role.Id equals rolePermission.RoleId
            join permission in dbContext.Permissions on rolePermission.PermissionId equals permission.Id
            where role.NormalizedName != null && normalizedRoles.Contains(role.NormalizedName)
            select permission.Name)
            .Distinct()
            .OrderBy(permission => permission)
            .ToArrayAsync(cancellationToken);

        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(CustomClaimTypes.FullName, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(
            permission => new Claim(CustomClaimTypes.Permission, permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresOn.UtcDateTime,
            signingCredentials: credentials);

        var response = new CurrentUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles.ToArray(),
            permissions);

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresOn,
            response);
    }

    public RefreshTokenResult CreateRefreshToken(string ipAddress)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresOn = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays);

        return new RefreshTokenResult(
            token,
            HashRefreshToken(token),
            expiresOn,
            ipAddress);
    }

    public static string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}

internal sealed record AccessTokenResult(
    string Token,
    DateTimeOffset ExpiresOn,
    CurrentUserResponse User);

internal sealed record RefreshTokenResult(
    string Token,
    string TokenHash,
    DateTimeOffset ExpiresOn,
    string CreatedByIp);
