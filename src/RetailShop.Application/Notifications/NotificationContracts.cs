using RetailShop.Application.Common;
using RetailShop.Domain.Notifications;

namespace RetailShop.Application.Notifications;

public sealed record NotificationMessageItem(
    Guid Id,
    NotificationKind Kind,
    NotificationChannel Channel,
    NotificationMessageStatus Status,
    string Title,
    string Message,
    string? RecipientName,
    string? RecipientPhone,
    DateTimeOffset GeneratedOn);

public sealed record BackgroundJobRunItem(
    Guid Id,
    string JobName,
    string RunKey,
    BackgroundJobRunStatus Status,
    DateTimeOffset StartedOn,
    DateTimeOffset? CompletedOn,
    int CreatedCount,
    string? Error);

public sealed record NotificationGenerationResult(
    string JobName,
    int CreatedCount);

public interface INotificationJobService
{
    Task<NotificationGenerationResult> GenerateDailyNotificationsAsync();

    Task<NotificationGenerationResult> GenerateLowStockAlertsAsync();

    Task<NotificationGenerationResult> GenerateDueRemindersAsync();

    Task<NotificationGenerationResult> GenerateWarrantyRemindersAsync();

    Task<NotificationGenerationResult> GenerateOrderNotificationsAsync();

    Task<PagedResult<NotificationMessageItem>> GetMessagesAsync(
        NotificationKind? kind,
        NotificationMessageStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResult<BackgroundJobRunItem>> GetJobRunsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<bool>> MarkCopiedAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<bool>> DismissAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);
}
