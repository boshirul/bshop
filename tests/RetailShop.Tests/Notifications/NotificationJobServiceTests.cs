using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RetailShop.Domain.Notifications;
using RetailShop.Infrastructure.Notifications;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.Notifications;

public sealed class NotificationJobServiceTests
{
    [Fact]
    public async Task GenerateLowStockAlertsAsync_DuplicateRunKeyIsAnIdempotentNoOp()
    {
        await using var context = CreateContext();
        var now = new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.Zero);
        context.BackgroundJobRuns.Add(new BackgroundJobRun("low-stock-alerts", "low-stock-alerts:20261016093000"));
        await context.SaveChangesAsync();

        var result = await CreateService(context, now).GenerateLowStockAlertsAsync();

        Assert.Equal(0, result.CreatedCount);
        Assert.Single(await context.BackgroundJobRuns.ToArrayAsync());
    }

    [Fact]
    public async Task GenerateDailyNotificationsAsync_UsesDhakaBusinessDayBoundaries()
    {
        await using var context = CreateContext();
        // 00:30 Dhaka on 16 Oct is still 15 Oct UTC. The previous business day is 15 Oct.
        var now = new DateTimeOffset(2026, 10, 15, 18, 30, 0, TimeSpan.Zero);

        await CreateService(context, now).GenerateDailyNotificationsAsync();

        Assert.Contains(await context.BackgroundJobRuns.ToArrayAsync(), run => run.JobName == "daily-sales-summary");
    }

    private static NotificationJobService CreateService(RetailShopDbContext context, DateTimeOffset now) =>
        new(context,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BusinessTimeZone:Id"] = "Asia/Dhaka"
            }).Build(),
            new FixedTimeProvider(now));

    private static RetailShopDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
