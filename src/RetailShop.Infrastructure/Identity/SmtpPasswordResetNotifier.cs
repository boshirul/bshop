using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RetailShop.Application.Authentication;

namespace RetailShop.Infrastructure.Identity;

internal sealed class SmtpPasswordResetNotifier(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<PasswordResetOptions> resetOptions,
    ILogger<SmtpPasswordResetNotifier> logger) : IPasswordResetNotifier
{
    public async Task SendAsync(
        string email,
        string displayName,
        string resetToken,
        CancellationToken cancellationToken)
    {
        var smtp = smtpOptions.Value;
        var reset = resetOptions.Value;
        Validate(smtp, reset);

        var link = BuildResetLink(reset.PublicApplicationUrl, email, resetToken);
        var encoder = HtmlEncoder.Default;
        using var message = new MailMessage
        {
            From = new MailAddress(smtp.SenderAddress, smtp.SenderDisplayName),
            Subject = "Reset your BShop password",
            IsBodyHtml = true,
            Body = $"""
                <!doctype html><html><body style="font-family:Arial,sans-serif;color:#172033;line-height:1.5">
                  <div style="max-width:560px;margin:auto;padding:24px;border:1px solid #dfe4ea;border-radius:12px">
                    <h1 style="font-size:22px;margin-top:0">Reset your BShop password</h1>
                    <p>Hello {encoder.Encode(displayName)},</p>
                    <p>We received a request to reset your BShop password.</p>
                    <p><a href="{encoder.Encode(link)}" style="display:inline-block;padding:12px 18px;background:#146c43;color:#fff;text-decoration:none;border-radius:6px">Choose a new password</a></p>
                    <p>This link expires in {reset.ExpirationMinutes} minutes and can be used only once.</p>
                    <p>If you did not request this change, you can safely ignore this email.</p>
                  </div>
                </body></html>
                """
        };
        message.To.Add(new MailAddress(email, displayName));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = checked(smtp.TimeoutSeconds * 1000)
        };
        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(smtp.TimeoutSeconds));
        try
        {
            await client.SendMailAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The SMTP operation timed out.");
        }
        logger.LogInformation("A password-reset email was accepted by the configured SMTP server.");
    }

    internal static string BuildResetLink(string publicApplicationUrl, string email, string token)
    {
        var baseUrl = publicApplicationUrl.TrimEnd('/');
        return $"{baseUrl}/auth/reset-password#email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    private static void Validate(SmtpOptions smtp, PasswordResetOptions reset)
    {
        if (!Uri.TryCreate(reset.PublicApplicationUrl, UriKind.Absolute, out var publicUri) ||
            (publicUri.Scheme != Uri.UriSchemeHttps && !publicUri.IsLoopback))
        {
            throw new InvalidOperationException("PasswordReset:PublicApplicationUrl must be an absolute HTTPS URL (or a loopback URL for local development).");
        }
        if (string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(smtp.SenderAddress))
        {
            throw new InvalidOperationException("SMTP email delivery is not configured.");
        }
        if (!string.IsNullOrWhiteSpace(smtp.Username) && string.IsNullOrEmpty(smtp.Password))
        {
            throw new InvalidOperationException("SMTP credentials are incomplete.");
        }
        if (smtp.TimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException("SMTP timeout must be between 1 and 120 seconds.");
        }
    }
}
