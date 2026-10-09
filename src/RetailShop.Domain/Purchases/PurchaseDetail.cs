using RetailShop.Domain.Products;

namespace RetailShop.Domain.Purchases;

public sealed class PurchaseDetail
{
    private PurchaseDetail()
    {
    }

    public PurchaseDetail(
        Guid purchaseId,
        Guid productId,
        decimal quantity,
        decimal unitCost,
        decimal discountAmount,
        decimal vatAmount)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }
        if (unitCost < 0 || discountAmount < 0 || vatAmount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitCost),
                "Costs, discounts, and VAT cannot be negative.");
        }
        if (discountAmount > quantity * unitCost)
        {
            throw new InvalidOperationException(
                "Line discount cannot exceed the gross amount.");
        }

        PurchaseId = purchaseId;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
        DiscountAmount = discountAmount;
        VatAmount = vatAmount;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid PurchaseId { get; private set; }
    public Purchase Purchase { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrossAmount => Quantity * UnitCost;
    public decimal LineTotal => GrossAmount - DiscountAmount + VatAmount;
    public decimal NetUnitCost => Quantity == 0 ? 0 : LineTotal / Quantity;

    public decimal RegisterReturn(decimal quantity)
    {
        if (quantity <= 0 || ReturnedQuantity + quantity > Quantity)
        {
            throw new InvalidOperationException(
                "Return quantity exceeds the remaining purchased quantity.");
        }

        ReturnedQuantity += quantity;
        return decimal.Round(NetUnitCost * quantity, 2, MidpointRounding.AwayFromZero);
    }
}
