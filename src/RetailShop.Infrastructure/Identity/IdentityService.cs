using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Authentication;
using RetailShop.Application.Common;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Application.Security;
using RetailShop.Application.Auditing;
using Microsoft.Extensions.Configuration;

namespace RetailShop.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TokenService tokenService,
    RetailShopDbContext dbContext,
    IPasswordResetNotifier passwordResetNotifier,
    IAuditService audit,
    IConfiguration configuration) : IIdentityService
{
    public async Task<OperationResult<AuthenticationSession>> LoginAsync(
        LoginRequest request,
        string ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null || !user.IsActive)
        {
            await WriteLoginLogAsync(
                null,
                normalizedEmail,
                false,
                "Invalid credentials or inactive account.",
                ipAddress,
                userAgent,
                cancellationToken);
            return OperationResult<AuthenticationSession>.Failure(
                "The email or password is incorrect.");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            await WriteLoginLogAsync(
                user.Id,
                normalizedEmail,
                false,
                signInResult.IsLockedOut ? "Account locked." : "Invalid password.",
                ipAddress,
                userAgent,
                cancellationToken);
            return OperationResult<AuthenticationSession>.Failure(
                signInResult.IsLockedOut
                    ? "The account is temporarily locked."
                    : "The email or password is incorrect.");
        }

        var session = await CreateSessionAsync(user, ipAddress, cancellationToken);
        await WriteLoginLogAsync(
            user.Id,
            normalizedEmail,
            true,
            null,
            ipAddress,
            userAgent,
            cancellationToken);

        return OperationResult<AuthenticationSession>.Success(session);
    }

    public async Task<OperationResult<AuthenticationSession>> RefreshAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var tokenHash = TokenService.HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

        if (storedToken is null || !storedToken.IsActive || !storedToken.User.IsActive)
        {
            return OperationResult<AuthenticationSession>.Failure(
                "The refresh token is invalid or expired.");
        }

        var replacement = tokenService.CreateRefreshToken(ipAddress);
        storedToken.RevokedOn = DateTimeOffset.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.RevocationReason = "Rotated";
        storedToken.ReplacedByTokenHash = replacement.TokenHash;

        var accessToken = await tokenService.CreateAccessTokenAsync(
            storedToken.User,
            cancellationToken);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            TokenHash = replacement.TokenHash,
            ExpiresOn = replacement.ExpiresOn,
            CreatedByIp = replacement.CreatedByIp
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<AuthenticationSession>.Success(
            CreateAuthenticationSession(accessToken, replacement));
    }

    public async Task LogoutAsync(
        string? refreshToken,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = TokenService.HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);

        if (storedToken is null || storedToken.RevokedOn is not null)
        {
            return;
        }

        storedToken.RevokedOn = DateTimeOffset.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.RevocationReason = "Logout";
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await passwordResetNotifier.SendAsync(
            user.Email ?? request.Email,
            token,
            cancellationToken);
    }

    public async Task<OperationResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return OperationResult<bool>.Failure("The reset request is invalid.");
        }

        var result = await userManager.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return OperationResult<bool>.Failure(
                result.Errors.Select(error => error.Description));
        }

        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id && token.RevokedOn == null)
            .ToArrayAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedOn = DateTimeOffset.UtcNow;
            token.RevocationReason = "Password reset";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<CurrentUserResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return OperationResult<CurrentUserResponse>.Failure("User not found.");
        }

        var token = await tokenService.CreateAccessTokenAsync(user, cancellationToken);
        return OperationResult<CurrentUserResponse>.Success(token.User);
    }

    public async Task<OperationResult<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return OperationResult<UserProfileResponse>.Failure("User not found.");
        var roles = await userManager.GetRolesAsync(user);
        return OperationResult<UserProfileResponse>.Success(new UserProfileResponse(user.Id, user.FullName, user.UserName ?? string.Empty, user.Email ?? string.Empty, user.IsActive, roles.ToArray(), string.IsNullOrWhiteSpace(user.AvatarStorageKey) ? null : "/api/auth/profile/photo/content"));
    }

    public async Task<OperationResult<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmNewPassword) return OperationResult<bool>.Failure("The new passwords do not match.");
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return OperationResult<bool>.Failure("User not found.");
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return OperationResult<bool>.Failure(result.Errors.Select(error => error.Description));
        var activeTokens = await dbContext.RefreshTokens.Where(token => token.UserId == userId && token.RevokedOn == null).ToArrayAsync(cancellationToken);
        foreach (var token in activeTokens) { token.RevokedOn = DateTimeOffset.UtcNow; token.RevokedByIp = ipAddress; token.RevocationReason = "Password changed"; }
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ChangePassword", "UserProfile", userId.ToString(), "User changed their password.", userId, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<string>> ReplaceAvatarAsync(Guid userId, Stream content, string contentType, long length, CancellationToken cancellationToken)
    {
        if (length <= 0 || length > 2 * 1024 * 1024) return OperationResult<string>.Failure("Profile photos must be 2 MB or smaller.");
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return OperationResult<string>.Failure("User not found.");
        var bytes = new byte[12]; var read = await content.ReadAsync(bytes, cancellationToken); content.Position = 0;
        var extension = GetImageExtension(bytes.AsSpan(0, read));
        if (extension is null) return OperationResult<string>.Failure("Only JPEG, PNG, and WebP image files are supported.");
        var root = configuration["MediaStorage:RootPath"] ?? Path.Combine("..", "RetailShop.Media");
        var directory = Path.Combine(Path.GetFullPath(root), "users", "avatars"); Directory.CreateDirectory(directory);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var key = Path.Combine("users", "avatars", $"{userId:N}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{suffix}{extension}").Replace('\\', '/');
        var absolute = Path.Combine(Path.GetFullPath(root), key.Replace('/', Path.DirectorySeparatorChar));
        await using (var file = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true)) await content.CopyToAsync(file, cancellationToken);
        var oldKey = user.AvatarStorageKey; user.AvatarStorageKey = key; await userManager.UpdateAsync(user); await audit.WriteAsync("ReplacePhoto", "UserProfile", userId.ToString(), "User replaced their profile photo.", userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(oldKey)) { var old = SafePath(root, oldKey); if (old is not null && File.Exists(old)) File.Delete(old); }
        return OperationResult<string>.Success("/api/auth/profile/photo/content");
    }

    public async Task<OperationResult<(Stream Content, string ContentType)>> GetAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.AvatarStorageKey)) return OperationResult<(Stream, string)>.Failure("Profile photo not found.");
        var root = configuration["MediaStorage:RootPath"] ?? Path.Combine("..", "RetailShop.Media"); var path = SafePath(root, user.AvatarStorageKey);
        if (path is null || !File.Exists(path)) return OperationResult<(Stream, string)>.Failure("Profile photo not found.");
        var type = Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return OperationResult<(Stream, string)>.Success((new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), type));
    }

    private static string? GetImageExtension(ReadOnlySpan<byte> bytes) => bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }) ? ".jpg" : bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ? ".png" : bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8) ? ".webp" : null;
    private static string? SafePath(string root, string key) { var fullRoot = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(fullRoot, key.Replace('/', Path.DirectorySeparatorChar))); return path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null; }

    private async Task<AuthenticationSession> CreateSessionAsync(
        ApplicationUser user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenService.CreateAccessTokenAsync(
            user,
            cancellationToken);
        var refreshToken = tokenService.CreateRefreshToken(ipAddress);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresOn = refreshToken.ExpiresOn,
            CreatedByIp = refreshToken.CreatedByIp
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthenticationSession(accessToken, refreshToken);
    }

    private static AuthenticationSession CreateAuthenticationSession(
        AccessTokenResult accessToken,
        RefreshTokenResult refreshToken) =>
        new(
            new AuthResponse(
                accessToken.Token,
                accessToken.ExpiresOn,
                accessToken.User),
            refreshToken.Token,
            refreshToken.ExpiresOn);

    private async Task WriteLoginLogAsync(
        Guid? userId,
        string email,
        bool succeeded,
        string? failureReason,
        string ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        dbContext.LoginLogs.Add(new LoginLog
        {
            UserId = userId,
            Email = email,
            Succeeded = succeeded,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
