using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailShop.Application.Auditing;
using RetailShop.Application.Purchases;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Products;
using RetailShop.Domain.Settings;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Infrastructure.Purchases;

namespace RetailShop.Tests.Purchases;

public sealed class PurchaseServiceTests
{
    [Fact]
    public async Task CreatePurchaseAsync_PersistsStockPaymentAndSupplierLedgerOnce()
    {
        await using var context = CreateContext();
        var supplier = new Supplier("SUP-000001", "Green Energy Eco Mart", "01700000000");
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var product = new Product("PRD-000001", "Solar Battery 100W", category.Id, unit.Id);
        var cash = new PaymentMethod("Cash", "CASH", "Cash");
        context.AddRange(supplier, category, unit, product, cash);
        await context.SaveChangesAsync();

        var service = new PurchaseService(context, new TestAuditService());
        var result = await service.CreatePurchaseAsync(
            new CreatePurchaseRequest(
                supplier.Id,
                "5824633101",
                new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero),
                null,
                [new CreatePurchaseItemRequest(product.Id, 60m, 15_000m, 0m, 0m)],
                [new CreatePurchasePaymentRequest(cash.Id, 900_000m, null, null)]),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(900_000m, result.Value!.GrandTotal);
        Assert.Equal(900_000m, result.Value.PaidAmount);
        Assert.Equal(0m, result.Value.DueAmount);
        Assert.Single(await context.Purchases.ToArrayAsync());
        Assert.Single(await context.PurchasePayments.ToArrayAsync());
        Assert.Single(await context.StockTransactions.ToArrayAsync());
        Assert.Equal(2, await context.SupplierLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task CreatePurchaseAsync_RejectsDuplicateSupplierInvoiceBeforePosting()
    {
        await using var context = CreateContext();
        var supplier = new Supplier("SUP-000001", "Green Energy Eco Mart", "01700000000");
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var product = new Product("PRD-000001", "Solar Battery 100W", category.Id, unit.Id);
        context.AddRange(supplier, category, unit, product);
        await context.SaveChangesAsync();

        var service = new PurchaseService(context, new TestAuditService());
        var request = new CreatePurchaseRequest(
            supplier.Id,
            "5824633101",
            new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero),
            null,
            [new CreatePurchaseItemRequest(product.Id, 1m, 15_000m, 0m, 0m)],
            []);

        Assert.True((await service.CreatePurchaseAsync(request, Guid.NewGuid(), CancellationToken.None)).Succeeded);
        var duplicate = await service.CreatePurchaseAsync(request, Guid.NewGuid(), CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Equal("This supplier invoice number has already been used.", duplicate.Errors.Single());
        Assert.Single(await context.Purchases.ToArrayAsync());
    }

    private static RetailShopDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class TestAuditService : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId,
            string? description, Guid? performedBy, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
