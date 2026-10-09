using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Contacts;
using RetailShop.Infrastructure.Contacts;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Tests.Contacts;

public sealed class ContactServiceTests
{
    [Fact]
    public async Task CreateCustomer_GeneratesCodeAndTrimsValues()
    {
        await using var context = CreateContext();
        var service = new CustomerService(context, new TestAuditService());

        var result = await service.CreateAsync(
            new SaveCustomerRequest(
                "  Amina Rahman  ",
                " 01700000000 ",
                "amina@example.com",
                "Dhaka",
                null,
                5000,
                true),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.StartsWith("CUS-", result.Value!.CustomerCode);
        Assert.Equal("Amina Rahman", result.Value.Name);
        Assert.Equal("01700000000", result.Value.Phone);
    }

    [Fact]
    public async Task CreateSupplier_GeneratesSupplierCode()
    {
        await using var context = CreateContext();
        var service = new SupplierService(context, new TestAuditService());

        var result = await service.CreateAsync(
            new SaveSupplierRequest(
                "Power House Ltd.",
                "Karim",
                "01800000000",
                null,
                null,
                null,
                true),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.StartsWith("SUP-", result.Value!.SupplierCode);
    }

    private static RetailShopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RetailShopDbContext(options);
    }

    private sealed class TestAuditService : IAuditService
    {
        public Task WriteAsync(
            string action,
            string entityType,
            string? entityId,
            string? description,
            Guid? performedBy,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
