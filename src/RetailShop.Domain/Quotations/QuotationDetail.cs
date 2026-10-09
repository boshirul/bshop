using RetailShop.Domain.Products;

namespace RetailShop.Domain.Quotations;

public sealed class QuotationDetail
{
    private QuotationDetail()
    {
    }

    public QuotationDetail(
        Guid quotationId,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal vatAmount)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }
        if (unitPrice < 0 || discountAmount < 0 || vatAmount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Price, discount, and VAT cannot be negative.");
        }
        if (discountAmount > quantity * unitPrice)
        {
            throw new InvalidOperationException(
                "Line discount cannot exceed the gross amount.");
        }

        QuotationId = quotationId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        VatAmount = vatAmount;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid QuotationId { get; private set; }
    public Quotation Quotation { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrossAmount => Quantity * UnitPrice;
    public decimal LineTotal => GrossAmount - DiscountAmount + VatAmount;
}
