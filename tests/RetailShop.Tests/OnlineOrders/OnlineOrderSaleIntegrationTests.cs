using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.OnlineOrders;
using RetailShop.Application.Sales;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Domain.Products;
using RetailShop.Domain.Sales;
using RetailShop.Infrastructure.OnlineOrders;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.OnlineOrders;

public sealed class OnlineOrderSaleIntegrationTests
{
    [Fact]
    public async Task PostSaleAsync_LinksDeliveredOrder_UsesSavedPriceAndDoesNotIssueStockAgain()
    {
        await using var context = CreateContext();
        var customer = new Customer("CUS-000001", "Abdul Awal", "01700000000");
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var product = new Product("PRD-000001", "Solar Battery 100W", category.Id, unit.Id)
        {
            SalePrice = 16_500m,
            AverageCost = 12_000m
        };
        var actor = Guid.NewGuid();
        var order = DeliveredOrder(product, actor);
        context.AddRange(customer, category, unit, product, order);
        await context.SaveChangesAsync();

        var sales = new CapturingSaleService(context);
        var service = new OnlineOrderService(context, new TestAuditService(), sales);

        var result = await service.PostSaleAsync(
            order.Id,
            new PostOnlineOrderSaleRequest(customer.Id),
            actor,
            canSellOnDue: true,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(sales.Request);
        Assert.Equal(customer.Id, sales.Request!.CustomerId);
        Assert.Equal(16_500m, sales.Request.Items.Single().UnitPrice);
        Assert.True(sales.StockAlreadyIssued);
        Assert.Equal(order.Id, sales.OnlineOrderId);
        Assert.Single(await context.Sales.Where(item => item.OnlineOrderId == order.Id).ToArrayAsync());

        var duplicate = await service.PostSaleAsync(
            order.Id,
            new PostOnlineOrderSaleRequest(customer.Id),
            actor,
            canSellOnDue: true,
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Equal("This online order is already linked to a sales invoice.", duplicate.Errors.Single());
        Assert.Equal(1, sales.CreateCalls);
    }

    [Fact]
    public async Task PostSaleAsync_RejectsDeliveryChargeUntilAnApprovedSalesTreatmentExists()
    {
        await using var context = CreateContext();
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var product = new Product("PRD-000001", "Solar Battery 100W", category.Id, unit.Id);
        var actor = Guid.NewGuid();
        var order = DeliveredOrder(product, actor, deliveryCharge: 100m);
        context.AddRange(category, unit, product, order);
        await context.SaveChangesAsync();

        var sales = new CapturingSaleService(context);
        var service = new OnlineOrderService(context, new TestAuditService(), sales);

        var result = await service.PostSaleAsync(
            order.Id,
            new PostOnlineOrderSaleRequest(Guid.NewGuid()),
            actor,
            canSellOnDue: true,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("delivery charge", result.Errors.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, sales.CreateCalls);
    }

    [Fact]
    public async Task PostSaleAsync_RejectsSerializedProductsBeforeCreatingASecondStockIssue()
    {
        await using var context = CreateContext();
        var category = new Category("Electrical");
        var unit = new Unit("Pieces", "pcs");
        var product = new Product("PRD-000001", "Serialized Battery", category.Id, unit.Id)
        {
            IsSerialRequired = true
        };
        var actor = Guid.NewGuid();
        var order = DeliveredOrder(product, actor);
        context.AddRange(category, unit, product, order);
        await context.SaveChangesAsync();

        var sales = new CapturingSaleService(context);
        var service = new OnlineOrderService(context, new TestAuditService(), sales);

        var result = await service.PostSaleAsync(
            order.Id,
            new PostOnlineOrderSaleRequest(Guid.NewGuid()),
            actor,
            canSellOnDue: true,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("serialized", result.Errors.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, sales.CreateCalls);
    }

    private static OnlineOrder DeliveredOrder(Product product, Guid actor, decimal deliveryCharge = 0m)
    {
        var order = new OnlineOrder(
            "WEB-TEST-000001",
            OnlineOrderSource.Website,
            "Abdul Awal",
            "01700000000",
            "Test address",
            deliveryCharge,
            null,
            actor);
        order.AddDetail(product.Id, 1m, 16_500m, 12_000m);
        order.Confirm(actor, null);
        order.Deliver(actor, null);
        return order;
    }

    private static RetailShopDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class CapturingSaleService(RetailShopDbContext context) : ISaleService
    {
        public CreateSaleRequest? Request { get; private set; }
        public Guid? OnlineOrderId { get; private set; }
        public bool StockAlreadyIssued { get; private set; }
        public int CreateCalls { get; private set; }

        public async Task<OperationResult<SaleDetailItem>> CreateSaleAsync(
            CreateSaleRequest request,
            Guid performedBy,
            bool canChangePrice,
            bool canDiscount,
            bool canSellOnDue,
            CancellationToken cancellationToken,
            Guid? onlineOrderId = null,
            bool stockAlreadyIssued = false)
        {
            Request = request;
            OnlineOrderId = onlineOrderId;
            StockAlreadyIssued = stockAlreadyIssued;
            CreateCalls++;
            var sale = new Sale("INV-TEST-000001", request.CustomerId, request.SaleDate, request.Notes);
            sale.LinkOnlineOrder(onlineOrderId!.Value);
            context.Sales.Add(sale);
            await context.SaveChangesAsync(cancellationToken);
            return OperationResult<SaleDetailItem>.Success(new SaleDetailItem(
                sale.Id, sale.InvoiceNumber, sale.CustomerId, "Abdul Awal", null, sale.SaleDate,
                sale.Status, sale.PaymentStatus, 0m, 0m, 0m, 0m, 0m, 0m, 0m, sale.Notes,
                null, null, [], [], sale.OnlineOrderId));
        }

        public Task<PagedResult<SaleListItem>> GetSalesAsync(string? search, Guid? customerId,
            SaleStatus? status, SalePaymentStatus? paymentStatus, int page, int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperationResult<SaleDetailItem>> GetSaleAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperationResult<InvoicePrintRepresentation>> GetInvoicePrintAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperationResult<SaleDetailItem>> RecordPaymentAsync(Guid saleId, RecordSalePaymentRequest request, Guid performedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperationResult<SaleDetailItem>> CancelSaleAsync(Guid saleId, CancelSaleRequest request, Guid performedBy, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperationResult<CustomerLedgerResult>> GetCustomerLedgerAsync(Guid customerId, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestAuditService : IAuditService
    {
        public Task WriteAsync(string action, string entityType, string? entityId, string? description,
            Guid? performedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
