using Microsoft.Extensions.Logging;
using RetailShop.Application.Authentication;

namespace RetailShop.Infrastructure.Identity;

internal sealed class LoggingPasswordResetNotifier(
    ILogger<LoggingPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public Task SendAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Password reset delivery is not configured. Development reset token for {Email}: {ResetToken}",
            email,
            resetToken);
        return Task.CompletedTask;
    }
}
