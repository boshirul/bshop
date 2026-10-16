using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Quotations;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Infrastructure.Quotations;

namespace RetailShop.Tests.Quotations;

public sealed class QuotationServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsDraftAndDoesNotCreateInventoryMovements()
    {
        await using var context = CreateContext();
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var customer = new Customer("CUS-000001", "Amina Rahman", "01700000000");
        var product = new Product("PRD-000001", "LED Panel Light", category.Id, unit.Id)
        {
            SalePrice = 250m
        };
        context.AddRange(category, unit, customer, product);
        await context.SaveChangesAsync();

        var service = new QuotationService(context, new TestAuditService());
        var quotationDate = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        var result = await service.CreateAsync(
            new SaveQuotationRequest(
                customer.Id,
                quotationDate,
                quotationDate.AddDays(7),
                "Customer requested a written quote.",
                null,
                [new SaveQuotationItemRequest(product.Id, 2, 250m, 0, 0)]),
            Guid.NewGuid(),
            canChangePrice: false,
            canDiscount: false,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(500m, result.Value!.GrandTotal);
        Assert.Single(result.Value.Items);
        Assert.Equal(250m, result.Value.Items.Single().UnitPrice);
        Assert.Equal(1, await context.Quotations.CountAsync());
        Assert.Empty(await context.StockTransactions.ToArrayAsync());
    }

    [Fact]
    public async Task CreateAsync_PersistsDiscountedQuotationWithoutChangingStock()
    {
        await using var context = CreateContext();
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var customer = new Customer("CUS-000001", "Surovi Rahman", "01700000000");
        var product = new Product("PRD-000001", "Solar Battery 100W", category.Id, unit.Id)
        {
            SalePrice = 16_500m
        };
        context.AddRange(category, unit, customer, product);
        await context.SaveChangesAsync();

        var service = new QuotationService(context, new TestAuditService());
        var quotationDate = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        var result = await service.CreateAsync(
            new SaveQuotationRequest(
                customer.Id,
                quotationDate,
                quotationDate.AddDays(7),
                null,
                null,
                [new SaveQuotationItemRequest(product.Id, 20m, 16_500m, 600m, 0m)]),
            Guid.NewGuid(),
            canChangePrice: false,
            canDiscount: true,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(329_400m, result.Value!.GrandTotal);
        Assert.Equal(600m, result.Value.DiscountAmount);
        Assert.Empty(await context.StockTransactions.ToArrayAsync());
    }

    private static RetailShopDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class TestAuditService : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId,
            string? description, Guid? performedBy, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
