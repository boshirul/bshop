using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Products;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Infrastructure.Products;

namespace RetailShop.Tests.Products;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task CreateProduct_GeneratesUniqueCodeAndValidEan13Barcode()
    {
        var options = new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new RetailShopDbContext(options);
        var category = new Category("Battery");
        var unit = new Unit("Pieces", "pcs");
        context.AddRange(category, unit);
        await context.SaveChangesAsync();

        var service = new ProductService(context, new TestAuditService());
        var result = await service.CreateProductAsync(
            new SaveProductRequest(
                "100Ah Solar Battery",
                category.Id,
                null,
                null,
                null,
                unit.Id,
                "12V",
                null,
                10000,
                12500,
                2,
                true,
                12,
                true,
                false,
                true),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.StartsWith("PRD-", result.Value!.ProductCode);
        Assert.Equal(13, result.Value.Barcode.Length);
        Assert.All(result.Value.Barcode, character => Assert.True(char.IsDigit(character)));
        Assert.True(IsValidEan13(result.Value.Barcode));
    }

    private static bool IsValidEan13(string barcode)
    {
        var sum = barcode[..12]
            .Select((character, index) =>
                (character - '0') * (index % 2 == 0 ? 1 : 3))
            .Sum();
        var expected = (10 - sum % 10) % 10;
        return barcode[12] - '0' == expected;
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
