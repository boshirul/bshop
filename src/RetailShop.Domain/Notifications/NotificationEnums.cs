namespace RetailShop.Domain.Notifications;

public enum NotificationKind
{
    LowStock = 1,
    DailySalesSummary = 2,
    CustomerDueReminder = 3,
    WarrantyExpiryReminder = 4,
    OnlineOrder = 5,
    JobFailure = 6
}

public enum NotificationChannel
{
    Sms = 1,
    WhatsApp = 2,
    Internal = 3
}

public enum NotificationMessageStatus
{
    Draft = 1,
    Copied = 2,
    SentExternally = 3,
    Dismissed = 4
}

public enum BackgroundJobRunStatus
{
    Running = 1,
    Completed = 2,
    Failed = 3
}
