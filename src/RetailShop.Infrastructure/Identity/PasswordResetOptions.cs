namespace RetailShop.Infrastructure.Identity;

internal sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";
    public int ExpirationMinutes { get; set; } = 30;
    public string PublicApplicationUrl { get; set; } = string.Empty;
}

internal sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderAddress { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = "BShop";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
}
