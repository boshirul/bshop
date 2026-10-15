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
    IReadOnlyCollection<string> Permissions,
    string? AvatarUrl = null);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresOn,
    CurrentUserResponse User);

public sealed record AuthenticationSession(
    AuthResponse Response,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresOn);

public sealed record UserProfileResponse(
    Guid Id,
    string FullName,
    string UserName,
    string Email,
    bool IsActive,
    IReadOnlyCollection<string> Roles,
    string? AvatarUrl);

public sealed record ChangePasswordRequest(
    [param: Required] string CurrentPassword,
    [param: Required, MinLength(8)] string NewPassword,
    [param: Required] string ConfirmNewPassword);

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

    Task<OperationResult<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<OperationResult<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<string>> ReplaceAvatarAsync(Guid userId, Stream content, string contentType, long length, CancellationToken cancellationToken);

    Task<OperationResult<(Stream Content, string ContentType)>> GetAvatarAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IPasswordResetNotifier
{
    Task SendAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken);
}
