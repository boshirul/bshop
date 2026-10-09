using RetailShop.Domain.Products;

namespace RetailShop.Tests.Products;

public sealed class ProductDomainTests
{
    [Fact]
    public void Category_NormalizesAndTrimsName()
    {
        var category = new Category("  Solar Battery  ");

        Assert.Equal("Solar Battery", category.Name);
        Assert.Equal("SOLAR BATTERY", category.NormalizedName);
    }

    [Fact]
    public void Unit_UpdatesNameSymbolAndNormalization()
    {
        var unit = new Unit(" Pieces ", " pcs ");

        Assert.Equal("Pieces", unit.Name);
        Assert.Equal("PIECES", unit.NormalizedName);
        Assert.Equal("pcs", unit.Symbol);
    }
}
