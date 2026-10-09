using RetailShop.Domain.Products;

namespace RetailShop.Domain.OnlineOrders;

public sealed class OnlineOrderDetail
{
    private OnlineOrderDetail()
    {
    }

    public OnlineOrderDetail(
        Guid onlineOrderId,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal costPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }
        if (unitPrice < 0 || costPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice),
                "Prices cannot be negative.");
        }

        OnlineOrderId = onlineOrderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        CostPrice = costPrice;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid OnlineOrderId { get; private set; }
    public OnlineOrder OnlineOrder { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;
}
