using RetailShop.Infrastructure.Identity;

namespace RetailShop.Tests.Security;

public sealed class RefreshTokenTests
{
    [Fact]
    public void HashRefreshToken_IsDeterministicAndDoesNotExposeToken()
    {
        const string token = "a-sensitive-refresh-token";

        var first = TokenService.HashRefreshToken(token);
        var second = TokenService.HashRefreshToken(token);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(token, first);
    }

    [Fact]
    public void IsActive_RequiresFutureExpiryAndNoRevocation()
    {
        var token = new RefreshToken
        {
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        Assert.True(token.IsActive);

        token.RevokedOn = DateTimeOffset.UtcNow;
        Assert.False(token.IsActive);
    }
}
