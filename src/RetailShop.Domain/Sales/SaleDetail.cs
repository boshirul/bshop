using RetailShop.Domain.Products;

namespace RetailShop.Domain.Sales;

public sealed class SaleDetail
{
    private SaleDetail()
    {
    }

    public SaleDetail(
        Guid saleId,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal vatAmount,
        decimal costPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }
        if (unitPrice < 0 || discountAmount < 0 || vatAmount < 0 || costPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Prices, discounts, VAT, and cost cannot be negative.");
        }
        if (discountAmount > quantity * unitPrice)
        {
            throw new InvalidOperationException(
                "Line discount cannot exceed the gross amount.");
        }

        SaleId = saleId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        VatAmount = vatAmount;
        CostPrice = costPrice;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal GrossAmount => Quantity * UnitPrice;
    public decimal LineTotal => GrossAmount - DiscountAmount + VatAmount;
    public decimal CostTotal => Quantity * CostPrice;
    public decimal Profit => LineTotal - CostTotal;
}
