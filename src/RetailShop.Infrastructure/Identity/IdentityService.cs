using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Authentication;
using RetailShop.Application.Common;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TokenService tokenService,
    RetailShopDbContext dbContext,
    IPasswordResetNotifier passwordResetNotifier) : IIdentityService
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
