using RetailShop.Domain.Products;

namespace RetailShop.Domain.Inventory;

public sealed class StockAdjustmentDetail
{
    private StockAdjustmentDetail()
    {
    }

    public StockAdjustmentDetail(
        Guid productId,
        StockBucket bucket,
        StockAdjustmentDirection direction,
        decimal quantity,
        decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Adjustment quantity must be greater than zero.");
        }

        if (unitCost < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitCost),
                "Unit cost cannot be negative.");
        }

        ProductId = productId;
        Bucket = bucket;
        Direction = direction;
        Quantity = quantity;
        UnitCost = unitCost;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid StockAdjustmentId { get; private set; }

    public StockAdjustment StockAdjustment { get; private set; } = null!;

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public StockBucket Bucket { get; private set; }

    public StockAdjustmentDirection Direction { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal AppliedCost { get; set; }
}
