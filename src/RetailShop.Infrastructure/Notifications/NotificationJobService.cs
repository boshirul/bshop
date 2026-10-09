using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Common;
using RetailShop.Application.Notifications;
using RetailShop.Domain.Notifications;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Domain.Sales;
using RetailShop.Domain.Warranty;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Notifications;

public sealed class NotificationJobService(RetailShopDbContext dbContext)
    : INotificationJobService
{
    public async Task<NotificationGenerationResult> GenerateDailyNotificationsAsync()
    {
        var total = 0;
        total += (await RunJobAsync(
            "low-stock-alerts",
            GenerateLowStockCoreAsync,
            CancellationToken.None)).CreatedCount;
        total += (await RunJobAsync(
            "customer-due-reminders",
            GenerateDueCoreAsync,
            CancellationToken.None)).CreatedCount;
        total += (await RunJobAsync(
            "warranty-reminders",
            GenerateWarrantyCoreAsync,
            CancellationToken.None)).CreatedCount;
        total += (await RunJobAsync(
            "online-order-notifications",
            GenerateOrdersCoreAsync,
            CancellationToken.None)).CreatedCount;
        total += (await RunJobAsync(
            "daily-sales-summary",
            GenerateDailySalesSummaryCoreAsync,
            CancellationToken.None)).CreatedCount;

        return new NotificationGenerationResult("daily-notifications", total);
    }

    public Task<NotificationGenerationResult> GenerateLowStockAlertsAsync() =>
        RunJobAsync("low-stock-alerts", GenerateLowStockCoreAsync, CancellationToken.None);

    public Task<NotificationGenerationResult> GenerateDueRemindersAsync() =>
        RunJobAsync("customer-due-reminders", GenerateDueCoreAsync, CancellationToken.None);

    public Task<NotificationGenerationResult> GenerateWarrantyRemindersAsync() =>
        RunJobAsync("warranty-reminders", GenerateWarrantyCoreAsync, CancellationToken.None);

    public Task<NotificationGenerationResult> GenerateOrderNotificationsAsync() =>
        RunJobAsync("online-order-notifications", GenerateOrdersCoreAsync, CancellationToken.None);

    public async Task<PagedResult<NotificationMessageItem>> GetMessagesAsync(
        NotificationKind? kind,
        NotificationMessageStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.NotificationMessages.AsNoTracking();
        if (kind.HasValue)
        {
            query = query.Where(message => message.Kind == kind.Value);
        }
        if (status.HasValue)
        {
            query = query.Where(message => message.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(message => message.GeneratedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new NotificationMessageItem(
                message.Id,
                message.Kind,
                message.Channel,
                message.Status,
                message.Title,
                message.Message,
                message.RecipientName,
                message.RecipientPhone,
                message.GeneratedOn))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<NotificationMessageItem>(items, page, pageSize, total);
    }

    public async Task<PagedResult<BackgroundJobRunItem>> GetJobRunsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.BackgroundJobRuns.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(run => run.StartedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(run => new BackgroundJobRunItem(
                run.Id,
                run.JobName,
                run.RunKey,
                run.Status,
                run.StartedOn,
                run.CompletedOn,
                run.CreatedCount,
                run.Error))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<BackgroundJobRunItem>(items, page, pageSize, total);
    }

    public async Task<OperationResult<bool>> MarkCopiedAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.NotificationMessages
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (message is null)
        {
            return OperationResult<bool>.Failure("Notification message not found.");
        }

        message.MarkCopied(performedBy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<bool>> DismissAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.NotificationMessages
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (message is null)
        {
            return OperationResult<bool>.Failure("Notification message not found.");
        }

        message.Dismiss(performedBy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private async Task<NotificationGenerationResult> RunJobAsync(
        string jobName,
        Func<CancellationToken, Task<int>> action,
        CancellationToken cancellationToken)
    {
        var run = new BackgroundJobRun(jobName, $"{jobName}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        dbContext.BackgroundJobRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var created = await action(cancellationToken);
            run.Complete(created);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new NotificationGenerationResult(jobName, created);
        }
        catch (Exception exception)
        {
            run.Fail(exception);
            await AddIfMissingAsync(
                NotificationKind.JobFailure,
                NotificationChannel.Internal,
                $"job-failure:{jobName}:{DateTimeOffset.UtcNow:yyyyMMdd}",
                $"Job failed: {jobName}",
                $"{jobName} failed: {exception.Message}",
                "Manager",
                null,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> GenerateLowStockCoreAsync(CancellationToken cancellationToken)
    {
        var enabled = await dbContext.SystemSettings
            .AsNoTracking()
            .OrderBy(setting => setting.CreatedOn)
            .Select(setting => setting.LowStockAlertsEnabled)
            .FirstOrDefaultAsync(cancellationToken);
        if (!enabled)
        {
            return 0;
        }

        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var rows = await dbContext.StockBalances
            .AsNoTracking()
            .Include(balance => balance.Product)
            .ThenInclude(product => product.Unit)
            .Where(balance =>
                balance.AvailableQuantity <= balance.Product.MinimumStockLevel)
            .Select(balance => new
            {
                balance.ProductId,
                balance.Product.ProductCode,
                ProductName = balance.Product.Name,
                balance.AvailableQuantity,
                balance.Product.MinimumStockLevel,
                Unit = balance.Product.Unit.Symbol
            })
            .ToArrayAsync(cancellationToken);

        var created = 0;
        foreach (var row in rows)
        {
            created += await AddIfMissingAsync(
                NotificationKind.LowStock,
                NotificationChannel.WhatsApp,
                $"low-stock:{today}:{row.ProductId}",
                $"Low stock: {row.ProductName}",
                $"Low stock alert: {row.ProductName} ({row.ProductCode}) has {row.AvailableQuantity:0.###} {row.Unit}; minimum is {row.MinimumStockLevel:0.###}. Please review purchase requirement.",
                "Manager",
                null,
                cancellationToken);
        }

        return created;
    }

    private async Task<int> GenerateDueCoreAsync(CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var rows = await dbContext.CustomerLedgerEntries
            .AsNoTracking()
            .GroupBy(entry => entry.CustomerId)
            .Select(group => new
            {
                CustomerId = group.Key,
                Balance = group.Sum(entry => entry.Debit) - group.Sum(entry => entry.Credit)
            })
            .Where(item => item.Balance > 0)
            .Join(
                dbContext.Customers.AsNoTracking(),
                balance => balance.CustomerId,
                customer => customer.Id,
                (balance, customer) => new
                {
                    customer.Id,
                    customer.Name,
                    customer.Phone,
                    balance.Balance
                })
            .OrderByDescending(item => item.Balance)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        var created = 0;
        foreach (var row in rows)
        {
            created += await AddIfMissingAsync(
                NotificationKind.CustomerDueReminder,
                NotificationChannel.Sms,
                $"due:{today}:{row.Id}",
                $"Due reminder: {row.Name}",
                $"Dear {row.Name}, your current due balance is BDT {row.Balance:0.00}. Please make payment at your earliest convenience. - KhanShop",
                row.Name,
                row.Phone,
                cancellationToken);
        }

        return created;
    }

    private async Task<int> GenerateWarrantyCoreAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(30);
        var rows = await dbContext.ProductSerials
            .AsNoTracking()
            .Include(serial => serial.Product)
            .Include(serial => serial.Customer)
            .Where(serial =>
                serial.Status == ProductSerialStatus.Sold &&
                serial.WarrantyExpiryDate != null &&
                serial.WarrantyExpiryDate >= today &&
                serial.WarrantyExpiryDate <= until &&
                serial.Customer != null)
            .Select(serial => new
            {
                serial.Id,
                serial.SerialNumber,
                ProductName = serial.Product.Name,
                serial.WarrantyExpiryDate,
                CustomerName = serial.Customer!.Name,
                serial.Customer.Phone
            })
            .ToArrayAsync(cancellationToken);

        var created = 0;
        foreach (var row in rows)
        {
            created += await AddIfMissingAsync(
                NotificationKind.WarrantyExpiryReminder,
                NotificationChannel.Sms,
                $"warranty-expiry:{row.WarrantyExpiryDate:yyyyMMdd}:{row.Id}",
                $"Warranty expiry: {row.ProductName}",
                $"Dear {row.CustomerName}, warranty for {row.ProductName} serial {row.SerialNumber} expires on {row.WarrantyExpiryDate:dd MMM yyyy}. Contact KhanShop if support is needed.",
                row.CustomerName,
                row.Phone,
                cancellationToken);
        }

        return created;
    }

    private async Task<int> GenerateOrdersCoreAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.OnlineOrders
            .AsNoTracking()
            .Where(order => order.Status == OnlineOrderStatus.Pending)
            .OrderBy(order => order.OrderedOn)
            .Take(100)
            .Select(order => new
            {
                order.Id,
                order.OrderNumber,
                order.CustomerName,
                order.CustomerPhone,
                order.GrandTotal
            })
            .ToArrayAsync(cancellationToken);

        var created = 0;
        foreach (var row in rows)
        {
            created += await AddIfMissingAsync(
                NotificationKind.OnlineOrder,
                NotificationChannel.WhatsApp,
                $"online-order:{row.Id}",
                $"Online order received: {row.OrderNumber}",
                $"Dear {row.CustomerName}, KhanShop received your order {row.OrderNumber}. Total: BDT {row.GrandTotal:0.00}. We will confirm availability shortly.",
                row.CustomerName,
                row.CustomerPhone,
                cancellationToken);
        }

        return created;
    }

    private async Task<int> GenerateDailySalesSummaryCoreAsync(CancellationToken cancellationToken)
    {
        var day = DateTimeOffset.UtcNow.Date.AddDays(-1);
        var nextDay = day.AddDays(1);
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Where(sale =>
                sale.SaleDate >= day &&
                sale.SaleDate < nextDay &&
                sale.Status != SaleStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Revenue = group.Sum(sale => sale.GrandTotal - sale.ReturnedAmount),
                Paid = group.Sum(sale => sale.PaidAmount - sale.RefundedAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sales is null || sales.Count == 0)
        {
            return 0;
        }

        return await AddIfMissingAsync(
            NotificationKind.DailySalesSummary,
            NotificationChannel.Internal,
            $"daily-sales:{day:yyyyMMdd}",
            $"Daily sales summary: {day:dd MMM yyyy}",
            $"Sales summary for {day:dd MMM yyyy}: {sales.Count} invoice(s), revenue BDT {sales.Revenue:0.00}, collected BDT {sales.Paid:0.00}.",
            "Manager",
            null,
            cancellationToken);
    }

    private async Task<int> AddIfMissingAsync(
        NotificationKind kind,
        NotificationChannel channel,
        string deduplicationKey,
        string title,
        string message,
        string? recipientName,
        string? recipientPhone,
        CancellationToken cancellationToken)
    {
        if (await dbContext.NotificationMessages.AnyAsync(
            item => item.DeduplicationKey == deduplicationKey,
            cancellationToken))
        {
            return 0;
        }

        dbContext.NotificationMessages.Add(new NotificationMessage(
            kind,
            channel,
            deduplicationKey,
            title,
            message,
            recipientName,
            recipientPhone));
        await dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }
}
