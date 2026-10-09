using RetailShop.Domain.Products;

namespace RetailShop.Domain.Inventory;

public sealed class StockBalance
{
    private StockBalance()
    {
    }

    public StockBalance(Guid productId)
    {
        ProductId = productId;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public decimal AvailableQuantity { get; private set; }

    public decimal ReservedQuantity { get; private set; }

    public decimal DamagedQuantity { get; private set; }

    public decimal WarrantyQuantity { get; private set; }

    public decimal SupplierClaimQuantity { get; private set; }

    public Guid Version { get; private set; } = Guid.NewGuid();

    public decimal TotalQuantity =>
        AvailableQuantity + ReservedQuantity + DamagedQuantity +
        WarrantyQuantity + SupplierClaimQuantity;

    public decimal GetQuantity(StockBucket bucket) =>
        bucket switch
        {
            StockBucket.Available => AvailableQuantity,
            StockBucket.Reserved => ReservedQuantity,
            StockBucket.Damaged => DamagedQuantity,
            StockBucket.Warranty => WarrantyQuantity,
            StockBucket.SupplierClaim => SupplierClaimQuantity,
            _ => throw new ArgumentOutOfRangeException(nameof(bucket))
        };

    public void Increase(StockBucket bucket, decimal quantity)
    {
        ValidateQuantity(quantity);
        SetQuantity(bucket, GetQuantity(bucket) + quantity);
        Touch();
    }

    public void Decrease(StockBucket bucket, decimal quantity)
    {
        ValidateQuantity(quantity);
        var current = GetQuantity(bucket);
        if (current < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient {bucket.ToString().ToLowerInvariant()} stock.");
        }

        SetQuantity(bucket, current - quantity);
        Touch();
    }

    public void Reserve(decimal quantity)
    {
        Decrease(StockBucket.Available, quantity);
        Increase(StockBucket.Reserved, quantity);
    }

    public void ReleaseReservation(decimal quantity)
    {
        Decrease(StockBucket.Reserved, quantity);
        Increase(StockBucket.Available, quantity);
    }

    private static void ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Stock quantity must be greater than zero.");
        }
    }

    private void SetQuantity(StockBucket bucket, decimal quantity)
    {
        switch (bucket)
        {
            case StockBucket.Available:
                AvailableQuantity = quantity;
                break;
            case StockBucket.Reserved:
                ReservedQuantity = quantity;
                break;
            case StockBucket.Damaged:
                DamagedQuantity = quantity;
                break;
            case StockBucket.Warranty:
                WarrantyQuantity = quantity;
                break;
            case StockBucket.SupplierClaim:
                SupplierClaimQuantity = quantity;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(bucket));
        }
    }

    private void Touch() => Version = Guid.NewGuid();
}
