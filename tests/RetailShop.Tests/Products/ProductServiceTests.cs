using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RetailShop.Application.Auditing;
using RetailShop.Application.Products;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Infrastructure.Products;

namespace RetailShop.Tests.Products;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task AddMediaAsync_UsesSafeProductBasedPersistentFilename()
    {
        var mediaRoot = Path.Combine(Path.GetTempPath(), "BShopTests", Guid.NewGuid().ToString("N"));
        try
        {
            var options = new DbContextOptionsBuilder<RetailShopDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var context = new RetailShopDbContext(options);
            var category = new Category("Television");
            var unit = new Unit("Pieces", "pcs");
            var product = new Product("PRD-000001", "Sony Bravia 43-inch", category.Id, unit.Id);
            context.AddRange(category, unit, product);
            await context.SaveChangesAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MediaStorage:RootPath"] = mediaRoot
                })
                .Build();
            var service = new ProductService(context, new TestAuditService(), configuration);
            await using var upload = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);

            var result = await service.AddMediaAsync(
                product.Id,
                new ProductMediaUpload(upload, "untrusted-name.jpg", "image/jpeg", upload.Length),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            var media = await context.ProductMedia.SingleAsync();
            Assert.Equal(Path.Combine("products", "images"), Path.GetDirectoryName(media.StorageKey));
            Assert.Matches("^sony-bravia-43-inch_\\d{8}_\\d{6}_\\d{3}_[a-f0-9]{6}\\.jpg$", Path.GetFileName(media.StorageKey));
            Assert.True(File.Exists(Path.Combine(mediaRoot, media.StorageKey)));
        }
        finally
        {
            if (Directory.Exists(mediaRoot)) Directory.Delete(mediaRoot, recursive: true);
        }
    }

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
