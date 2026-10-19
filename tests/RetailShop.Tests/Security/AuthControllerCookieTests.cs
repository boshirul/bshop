using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RetailShop.Api.Controllers;
using RetailShop.Application.Authentication;
using RetailShop.Application.Common;
using RetailShop.Shared.Contracts;

namespace RetailShop.Tests.Security;

public sealed class AuthControllerCookieTests
{
    [Theory]
    [InlineData("", "/api/auth")]
    [InlineData("/BShopApi", "/BShopApi/api/auth")]
    public async Task Login_ScopesRefreshCookieToThePublicApiPath(
        string pathBase,
        string expectedPath)
    {
        var identity = new TestIdentityService
        {
            LoginResult = OperationResult<AuthenticationSession>.Success(CreateSession())
        };
        var context = CreateContext(pathBase);
        var controller = CreateController(identity, context);

        var result = await controller.Login(
            new LoginRequest("cashier@example.test", "not-a-real-password"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        AssertCookiePath(context, expectedPath, requiresSecurityAttributes: true);
    }

    [Fact]
    public async Task RefreshTokenFailure_DeletesCookieUsingTheIisPath()
    {
        var identity = new TestIdentityService
        {
            RefreshResult = OperationResult<AuthenticationSession>.Failure("Refresh token has expired.")
        };
        var context = CreateContext("/BShopApi");
        context.Request.Headers.Cookie = "khanshop.refresh=expired-token";
        var controller = CreateController(identity, context);

        var result = await controller.RefreshToken(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        AssertCookiePath(context, "/BShopApi/api/auth");
    }

    [Fact]
    public async Task RefreshTokenSuccess_RenewsCookieUsingTheIisPath()
    {
        var identity = new TestIdentityService
        {
            RefreshResult = OperationResult<AuthenticationSession>.Success(CreateSession())
        };
        var context = CreateContext("/BShopApi");
        context.Request.Headers.Cookie = "khanshop.refresh=active-token";
        var controller = CreateController(identity, context);

        var result = await controller.RefreshToken(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        AssertCookiePath(context, "/BShopApi/api/auth", requiresSecurityAttributes: true);
    }

    [Fact]
    public async Task Logout_DeletesCookieUsingTheDevelopmentPath()
    {
        var context = CreateContext(string.Empty);
        context.Request.Headers.Cookie = "khanshop.refresh=active-token";
        var controller = CreateController(new TestIdentityService(), context);

        var result = await controller.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        AssertCookiePath(context, "/api/auth");
    }

    [Fact]
    public async Task PasswordChange_DeletesCookieUsingTheIisPath()
    {
        var identity = new TestIdentityService
        {
            ChangePasswordResult = OperationResult<bool>.Success(true)
        };
        var context = CreateContext("/BShopApi");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "test"));
        var controller = CreateController(identity, context);

        var result = await controller.ChangePassword(
            new ChangePasswordRequest("current", "new-password", "new-password"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        AssertCookiePath(context, "/BShopApi/api/auth");
    }

    [Theory]
    [InlineData("registered@example.test")]
    [InlineData("unknown@example.test")]
    public async Task ForgotPassword_AlwaysReturnsTheSameGenericResponse(string email)
    {
        var controller = CreateController(new TestIdentityService(), CreateContext(string.Empty));
        var result = await controller.ForgotPassword(new ForgotPasswordRequest(email), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<string>>(ok.Value);
        Assert.Equal("If an account exists for this email address, password reset instructions will be sent.", response.Data);
        Assert.DoesNotContain(email, response.Data, StringComparison.OrdinalIgnoreCase);
    }

    private static AuthController CreateController(
        IIdentityService identityService,
        DefaultHttpContext context) =>
        new(identityService)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

    private static DefaultHttpContext CreateContext(string pathBase)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = pathBase;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return context;
    }

    private static void AssertCookiePath(
        HttpContext context,
        string expectedPath,
        bool requiresSecurityAttributes = false)
    {
        var setCookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.Contains($"path={expectedPath}", setCookie, StringComparison.OrdinalIgnoreCase);
        if (requiresSecurityAttributes)
        {
            Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static AuthenticationSession CreateSession() =>
        new(
            new AuthResponse(
                "access-token-not-used-by-this-test",
                DateTimeOffset.UtcNow.AddMinutes(15),
                new CurrentUserResponse(
                    Guid.NewGuid(),
                    "cashier@example.test",
                    "Cashier",
                    [],
                    [])),
            "refresh-token-not-used-by-this-test",
            DateTimeOffset.UtcNow.AddDays(7));

    private sealed class TestIdentityService : IIdentityService
    {
        public OperationResult<AuthenticationSession>? LoginResult { get; init; }
        public OperationResult<AuthenticationSession>? RefreshResult { get; init; }
        public OperationResult<bool>? ChangePasswordResult { get; init; }

        public Task<OperationResult<AuthenticationSession>> LoginAsync(LoginRequest request, string ipAddress, string? userAgent, CancellationToken cancellationToken) =>
            Task.FromResult(LoginResult ?? OperationResult<AuthenticationSession>.Failure("Not configured."));

        public Task<OperationResult<AuthenticationSession>> RefreshAsync(string refreshToken, string ipAddress, CancellationToken cancellationToken) =>
            Task.FromResult(RefreshResult ?? OperationResult<AuthenticationSession>.Failure("Not configured."));

        public Task LogoutAsync(string? refreshToken, string ipAddress, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OperationResult<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken) => Task.FromResult(OperationResult<bool>.Failure("Not configured."));
        public Task<OperationResult<CurrentUserResponse>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(OperationResult<CurrentUserResponse>.Failure("Not configured."));
        public Task<OperationResult<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(OperationResult<UserProfileResponse>.Failure("Not configured."));
        public Task<OperationResult<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string ipAddress, CancellationToken cancellationToken) => Task.FromResult(ChangePasswordResult ?? OperationResult<bool>.Failure("Not configured."));
        public Task<OperationResult<string>> ReplaceAvatarAsync(Guid userId, Stream content, string contentType, long length, CancellationToken cancellationToken) => Task.FromResult(OperationResult<string>.Failure("Not configured."));
        public Task<OperationResult<(Stream Content, string ContentType)>> GetAvatarAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(OperationResult<(Stream Content, string ContentType)>.Failure("Not configured."));
    }
}
