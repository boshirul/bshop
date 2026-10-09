using RetailShop.Domain.Common;

namespace RetailShop.Domain.Notifications;

public sealed class NotificationMessage : AuditableEntity
{
    private NotificationMessage()
    {
    }

    public NotificationMessage(
        NotificationKind kind,
        NotificationChannel channel,
        string deduplicationKey,
        string title,
        string message,
        string? recipientName,
        string? recipientPhone)
    {
        Kind = kind;
        Channel = channel;
        DeduplicationKey = deduplicationKey.Trim();
        Title = title.Trim();
        Message = message.Trim();
        RecipientName = Clean(recipientName);
        RecipientPhone = Clean(recipientPhone);
    }

    public NotificationKind Kind { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public NotificationMessageStatus Status { get; private set; } =
        NotificationMessageStatus.Draft;

    public string DeduplicationKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? RecipientName { get; private set; }

    public string? RecipientPhone { get; private set; }

    public DateTimeOffset GeneratedOn { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StatusChangedOn { get; private set; }

    public void MarkCopied(Guid performedBy)
    {
        Status = NotificationMessageStatus.Copied;
        StatusChangedOn = DateTimeOffset.UtcNow;
        LastModifiedBy = performedBy;
    }

    public void Dismiss(Guid performedBy)
    {
        Status = NotificationMessageStatus.Dismissed;
        StatusChangedOn = DateTimeOffset.UtcNow;
        LastModifiedBy = performedBy;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
