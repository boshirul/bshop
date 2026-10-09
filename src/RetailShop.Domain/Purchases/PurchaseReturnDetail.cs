namespace RetailShop.Domain.Purchases;

public sealed class PurchaseReturnDetail
{
    private PurchaseReturnDetail()
    {
    }

    public PurchaseReturnDetail(
        Guid purchaseReturnId,
        Guid purchaseDetailId,
        decimal quantity,
        decimal amount)
    {
        if (quantity <= 0 || amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Return quantity must be positive and amount cannot be negative.");
        }

        PurchaseReturnId = purchaseReturnId;
        PurchaseDetailId = purchaseDetailId;
        Quantity = quantity;
        Amount = amount;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid PurchaseReturnId { get; private set; }
    public PurchaseReturn PurchaseReturn { get; private set; } = null!;
    public Guid PurchaseDetailId { get; private set; }
    public PurchaseDetail PurchaseDetail { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal Amount { get; private set; }
}
