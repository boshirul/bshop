using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;

namespace RetailShop.Application.Authentication;

public sealed record LoginRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required] string Password);

public sealed record ForgotPasswordRequest(
    [param: Required, EmailAddress] string Email);

public sealed record ResetPasswordRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required] string Token,
    [param: Required, MinLength(8)] string NewPassword);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresOn,
    CurrentUserResponse User);

public sealed record AuthenticationSession(
    AuthResponse Response,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresOn);

public interface IIdentityService
{
    Task<OperationResult<AuthenticationSession>> LoginAsync(
        LoginRequest request,
        string ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    Task<OperationResult<AuthenticationSession>> RefreshAsync(
        string refreshToken,
        string ipAddress,
        CancellationToken cancellationToken);

    Task LogoutAsync(
        string? refreshToken,
        string ipAddress,
        CancellationToken cancellationToken);

    Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken);

    Task<OperationResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken);

    Task<OperationResult<CurrentUserResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public interface IPasswordResetNotifier
{
    Task SendAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken);
}
