using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailShop.Api.Infrastructure;
using RetailShop.Application.Common;
using RetailShop.Application.Notifications;
using RetailShop.Application.Security;
using RetailShop.Domain.Notifications;
using RetailShop.Shared.Contracts;

namespace RetailShop.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = Permissions.Notifications.View)]
public sealed class NotificationsController(INotificationJobService service)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationMessageItem>>>>
        Messages(
            [FromQuery] NotificationKind? kind,
            [FromQuery] NotificationMessageStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<NotificationMessageItem>>.Success(
            await service.GetMessagesAsync(kind, status, page, pageSize, cancellationToken)));

    [HttpGet("jobs")]
    public async Task<ActionResult<ApiResponse<PagedResult<BackgroundJobRunItem>>>>
        Jobs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<BackgroundJobRunItem>>.Success(
            await service.GetJobRunsAsync(page, pageSize, cancellationToken)));

    [HttpPost("generate/daily")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<NotificationGenerationResult>>>
        GenerateDaily() =>
        Ok(ApiResponse<NotificationGenerationResult>.Success(
            await service.GenerateDailyNotificationsAsync()));

    [HttpPost("generate/low-stock")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<NotificationGenerationResult>>>
        GenerateLowStock() =>
        Ok(ApiResponse<NotificationGenerationResult>.Success(
            await service.GenerateLowStockAlertsAsync()));

    [HttpPost("generate/dues")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<NotificationGenerationResult>>>
        GenerateDues() =>
        Ok(ApiResponse<NotificationGenerationResult>.Success(
            await service.GenerateDueRemindersAsync()));

    [HttpPost("generate/warranty")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<NotificationGenerationResult>>>
        GenerateWarranty() =>
        Ok(ApiResponse<NotificationGenerationResult>.Success(
            await service.GenerateWarrantyRemindersAsync()));

    [HttpPost("generate/orders")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<NotificationGenerationResult>>>
        GenerateOrders() =>
        Ok(ApiResponse<NotificationGenerationResult>.Success(
            await service.GenerateOrderNotificationsAsync()));

    [HttpPost("{id:guid}/copied")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> MarkCopied(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.MarkCopiedAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : NotFound(ApiResponse<bool>.Failure(result.Errors));
    }

    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Policy = Permissions.Notifications.Manage)]
    public async Task<ActionResult<ApiResponse<bool>>> Dismiss(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DismissAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.Succeeded
            ? Ok(ApiResponse<bool>.Success(true))
            : NotFound(ApiResponse<bool>.Failure(result.Errors));
    }
}
