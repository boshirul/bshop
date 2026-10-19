using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RetailShop.Application.Auditing;
using RetailShop.Application.Authentication;
using RetailShop.Api.Controllers;
using RetailShop.Infrastructure.Identity;
using RetailShop.Infrastructure.Persistence;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RetailShop.Tests.Security;

public sealed class PasswordRecoveryTests
{
    private const string Email = "user@example.test";
    private const string OldPassword = "OldPassword1!";
    private const string NewPassword = "NewPassword2!";

    [Fact]
    public async Task RegisteredEmail_ReceivesToken_AndUnknownEmailDoesNot()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest(Email), CancellationToken.None);
        Assert.NotNull(fixture.Notifier.Token);
        Assert.Equal(Email, fixture.Notifier.Email);

        fixture.Notifier.Clear();
        await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest("unknown@example.test"), CancellationToken.None);
        Assert.Null(fixture.Notifier.Token);
    }

    [Fact]
    public async Task ValidToken_ChangesPassword_RevokesSessions_AndCannotBeReused()
    {
        await using var fixture = await Fixture.CreateAsync(withRefreshToken: true);
        await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest(Email), CancellationToken.None);
        var request = new ResetPasswordRequest(Email, fixture.Notifier.Token!, NewPassword, NewPassword);

        var result = await fixture.Service.ResetPasswordAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        var user = await fixture.Users.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.False(await fixture.Users.CheckPasswordAsync(user!, OldPassword));
        Assert.True(await fixture.Users.CheckPasswordAsync(user!, NewPassword));
        var session = await fixture.Database.RefreshTokens.SingleAsync();
        Assert.NotNull(session.RevokedOn);
        Assert.Equal("Password reset", session.RevocationReason);
        Assert.False((await fixture.Service.ResetPasswordAsync(request, CancellationToken.None)).Succeeded);
    }

    [Fact]
    public async Task InvalidAndExpiredTokensAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync(tokenLifetime: TimeSpan.FromMilliseconds(100));
        var invalid = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(Email, "invalid-token", NewPassword, NewPassword), CancellationToken.None);
        Assert.False(invalid.Succeeded);

        await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest(Email), CancellationToken.None);
        await Task.Delay(1100);
        var expired = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(Email, fixture.Notifier.Token!, NewPassword, NewPassword), CancellationToken.None);
        Assert.False(expired.Succeeded);
    }

    [Fact]
    public async Task InvalidPasswordAndConfirmationMismatchAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest(Email), CancellationToken.None);
        var weak = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(Email, fixture.Notifier.Token!, "weak", "weak"), CancellationToken.None);
        var mismatch = await fixture.Service.ResetPasswordAsync(
            new ResetPasswordRequest(Email, fixture.Notifier.Token!, NewPassword, "Different3!"), CancellationToken.None);
        Assert.False(weak.Succeeded);
        Assert.False(mismatch.Succeeded);
        Assert.Contains("match", mismatch.Errors.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmailProviderFailureIsControlledAndAuditedWithoutTokenDisclosure()
    {
        await using var fixture = await Fixture.CreateAsync(failDelivery: true);
        await fixture.Service.ForgotPasswordAsync(new ForgotPasswordRequest(Email), CancellationToken.None);
        var audit = Assert.Single(fixture.Audit.Events, item => item.Action == "PasswordResetDeliveryFailed");
        Assert.DoesNotContain(fixture.Notifier.Token ?? string.Empty, audit.Description ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetLinkUsesConfiguredBShopOriginAndFragment()
    {
        var link = SmtpPasswordResetNotifier.BuildResetLink("https://shop.example.com/BShop/", "user+one@example.test", "token+/=");
        Assert.Equal("https://shop.example.com/BShop/auth/reset-password#email=user%2Bone%40example.test&token=token%2B%2F%3D", link);
    }

    [Fact]
    public async Task MissingEmailConfigurationFailsWithoutSending()
    {
        var notifier = new SmtpPasswordResetNotifier(
            Options.Create(new SmtpOptions()),
            Options.Create(new PasswordResetOptions { PublicApplicationUrl = "https://shop.example.com/BShop" }),
            NullLogger<SmtpPasswordResetNotifier>.Instance);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            notifier.SendAsync(Email, "User", "secret-token", CancellationToken.None));
        Assert.Equal("SMTP email delivery is not configured.", error.Message);
        Assert.DoesNotContain("secret-token", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void ForgotPasswordRequest_RejectsMissingOrInvalidEmail(string email)
    {
        var parameter = typeof(ForgotPasswordRequest).GetConstructors().Single().GetParameters().Single();
        var validators = parameter.GetCustomAttributes<ValidationAttribute>().ToArray();
        Assert.NotEmpty(validators);
        Assert.Contains(validators, validator => !validator.IsValid(email));
    }

    [Theory]
    [InlineData(nameof(AuthController.ForgotPassword))]
    [InlineData(nameof(AuthController.ResetPassword))]
    public void PasswordRecoveryEndpointsUseDedicatedRateLimit(string methodName)
    {
        var method = typeof(AuthController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        var attribute = Assert.Single(method.GetCustomAttributes<EnableRateLimitingAttribute>());
        Assert.Equal("PasswordRecovery", attribute.PolicyName);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly AsyncServiceScope scope;
        public IdentityService Service { get; }
        public UserManager<ApplicationUser> Users { get; }
        public RetailShopDbContext Database { get; }
        public FakeNotifier Notifier { get; }
        public FakeAuditService Audit { get; }

        private Fixture(ServiceProvider provider, AsyncServiceScope scope)
        {
            this.provider = provider;
            this.scope = scope;
            Service = scope.ServiceProvider.GetRequiredService<IdentityService>();
            Users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Database = scope.ServiceProvider.GetRequiredService<RetailShopDbContext>();
            Notifier = scope.ServiceProvider.GetRequiredService<FakeNotifier>();
            Audit = scope.ServiceProvider.GetRequiredService<FakeAuditService>();
        }

        public static async Task<Fixture> CreateAsync(
            bool withRefreshToken = false,
            bool failDelivery = false,
            TimeSpan? tokenLifetime = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthentication();
            services.AddHttpContextAccessor();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddDbContext<RetailShopDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.User.RequireUniqueEmail = true;
                })
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<RetailShopDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();
            services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = tokenLifetime ?? TimeSpan.FromMinutes(30));
            services.Configure<JwtOptions>(options =>
            {
                options.Issuer = "test"; options.Audience = "test";
                options.Key = "test-signing-key-with-more-than-thirty-two-bytes";
            });
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(new FakeNotifier { Fail = failDelivery });
            services.AddSingleton<IPasswordResetNotifier>(provider => provider.GetRequiredService<FakeNotifier>());
            services.AddSingleton<FakeAuditService>();
            services.AddSingleton<IAuditService>(provider => provider.GetRequiredService<FakeAuditService>());
            services.AddScoped<TokenService>();
            services.AddScoped<IdentityService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var fixture = new Fixture(provider, scope);
            var user = new ApplicationUser { UserName = Email, Email = Email, EmailConfirmed = true, FullName = "Test User", IsActive = true };
            Assert.True((await fixture.Users.CreateAsync(user, OldPassword)).Succeeded);
            if (withRefreshToken)
            {
                fixture.Database.RefreshTokens.Add(new RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = new string('a', 64),
                    CreatedByIp = "127.0.0.1",
                    ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
                });
                await fixture.Database.SaveChangesAsync();
            }
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class FakeNotifier : IPasswordResetNotifier
    {
        public bool Fail { get; init; }
        public string? Email { get; private set; }
        public string? Token { get; private set; }
        public Task SendAsync(string email, string displayName, string resetToken, CancellationToken cancellationToken)
        {
            Email = email; Token = resetToken;
            return Fail ? Task.FromException(new InvalidOperationException("SMTP unavailable")) : Task.CompletedTask;
        }
        public void Clear() { Email = null; Token = null; }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public List<(string Action, string? Description)> Events { get; } = [];
        public Task WriteAsync(string action, string entityType, string? entityId, string? description, Guid? performedBy, CancellationToken cancellationToken = default)
        {
            Events.Add((action, description));
            return Task.CompletedTask;
        }
    }
}
