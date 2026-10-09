using Microsoft.EntityFrameworkCore;
using RetailShop.Infrastructure.Identity;
using RetailShop.Infrastructure.Persistence;
using RetailShop.Domain.Products;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Settings;
using RetailShop.Domain.Inventory;

namespace RetailShop.Tests.Infrastructure;

public sealed class IdentityModelTests
{
    [Fact]
    public void IdentityModel_UsesExpectedBusinessTableNames()
    {
        var options = new DbContextOptionsBuilder<RetailShopDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new RetailShopDbContext(options);

        Assert.Equal(
            "Users",
            context.Model.FindEntityType(typeof(ApplicationUser))?.GetTableName());
        Assert.Equal(
            "Roles",
            context.Model.FindEntityType(typeof(ApplicationRole))?.GetTableName());
        Assert.Equal(
            "Permissions",
            context.Model.FindEntityType(typeof(PermissionRecord))?.GetTableName());
        Assert.Equal(
            "Products",
            context.Model.FindEntityType(typeof(Product))?.GetTableName());
        Assert.Equal(
            "ProductBarcodes",
            context.Model.FindEntityType(typeof(ProductBarcode))?.GetTableName());
        Assert.Equal(
            "Customers",
            context.Model.FindEntityType(typeof(Customer))?.GetTableName());
        Assert.Equal(
            "Suppliers",
            context.Model.FindEntityType(typeof(Supplier))?.GetTableName());
        Assert.Equal(
            "ShopSettings",
            context.Model.FindEntityType(typeof(ShopSetting))?.GetTableName());
        Assert.Equal(
            "StockTransactions",
            context.Model.FindEntityType(typeof(StockTransaction))?.GetTableName());
        Assert.Equal(
            "StockBalances",
            context.Model.FindEntityType(typeof(StockBalance))?.GetTableName());
    }
}
