using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Authentication;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("Authentication")]
public sealed class AuthController(IIdentityService identityService) : ControllerBase
{
    private const string RefreshCookieName = "khanshop.refresh";

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.LoginAsync(
            request,
            GetIpAddress(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (!result.Succeeded)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Failure(result.Errors));
        }

        WriteRefreshCookie(result.Value!);
        return Ok(ApiResponse<AuthResponse>.Success(result.Value!.Response));
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken(
        CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(
                ApiResponse<AuthResponse>.Failure(["Refresh token is missing."]));
        }

        var result = await identityService.RefreshAsync(
            refreshToken,
            GetIpAddress(),
            cancellationToken);

        if (!result.Succeeded)
        {
            DeleteRefreshCookie();
            return Unauthorized(ApiResponse<AuthResponse>.Failure(result.Errors));
        }

        WriteRefreshCookie(result.Value!);
        return Ok(ApiResponse<AuthResponse>.Success(result.Value!.Response));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
        await identityService.LogoutAsync(
            refreshToken,
            GetIpAddress(),
            cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType<ApiResponse<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<string>>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await identityService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<string>.Success(
            "If the account exists, password reset instructions have been sent."));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType<ApiResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<bool>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await identityService.ResetPasswordAsync(
            request,
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true, "Password reset successfully."))
            : BadRequest(ApiResponse<bool>.Failure(result.Errors));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<ApiResponse<CurrentUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Me(
        CancellationToken cancellationToken)
    {
        var result = await identityService.GetCurrentUserAsync(
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<CurrentUserResponse>.Success(result.Value!))
            : Unauthorized(ApiResponse<CurrentUserResponse>.Failure(result.Errors));
    }

    private void WriteRefreshCookie(AuthenticationSession session)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = session.RefreshTokenExpiresOn,
                Path = "/api/auth",
                IsEssential = true
            });
    }

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth"
            });

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
