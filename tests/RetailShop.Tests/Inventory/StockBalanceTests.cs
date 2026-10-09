using RetailShop.Domain.Inventory;

namespace RetailShop.Tests.Inventory;

public sealed class StockBalanceTests
{
    [Fact]
    public void Reserve_MovesAvailableStockWithoutChangingTotal()
    {
        var balance = new StockBalance(Guid.NewGuid());
        balance.Increase(StockBucket.Available, 10);

        balance.Reserve(3);

        Assert.Equal(7, balance.AvailableQuantity);
        Assert.Equal(3, balance.ReservedQuantity);
        Assert.Equal(10, balance.TotalQuantity);
    }

    [Fact]
    public void Decrease_PreventsNegativeStock()
    {
        var balance = new StockBalance(Guid.NewGuid());
        balance.Increase(StockBucket.Available, 2);

        var exception = Assert.Throws<InvalidOperationException>(
            () => balance.Decrease(StockBucket.Available, 3));

        Assert.Contains("Insufficient available stock", exception.Message);
        Assert.Equal(2, balance.AvailableQuantity);
    }

    [Fact]
    public void ReleaseReservation_RestoresAvailableStock()
    {
        var balance = new StockBalance(Guid.NewGuid());
        balance.Increase(StockBucket.Available, 8);
        balance.Reserve(5);

        balance.ReleaseReservation(2);

        Assert.Equal(5, balance.AvailableQuantity);
        Assert.Equal(3, balance.ReservedQuantity);
    }
}
