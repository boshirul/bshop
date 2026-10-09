using RetailShop.Domain.Sales;

namespace RetailShop.Domain.Returns;

public sealed class SalesReturnDetail
{
    private SalesReturnDetail()
    {
    }

    public SalesReturnDetail(
        Guid salesReturnId,
        Guid saleDetailId,
        decimal quantity,
        decimal amount)
    {
        if (quantity <= 0 || amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Return quantity must be positive and amount cannot be negative.");
        }
        SalesReturnId = salesReturnId;
        SaleDetailId = saleDetailId;
        Quantity = quantity;
        Amount = amount;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid SalesReturnId { get; private set; }
    public SalesReturn SalesReturn { get; private set; } = null!;
    public Guid SaleDetailId { get; private set; }
    public SaleDetail SaleDetail { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal Amount { get; private set; }
}
