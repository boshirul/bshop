namespace RetailShop.Infrastructure.Identity;

public sealed class LoginLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid? UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public string? FailureReason { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string? UserAgent { get; set; }

    public DateTimeOffset OccurredOn { get; set; } = DateTimeOffset.UtcNow;
}
