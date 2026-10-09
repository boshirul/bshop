using RetailShop.Domain.Sales;

namespace RetailShop.Domain.CustomerAccounts;

public sealed class CustomerReceiptAllocation
{
    private CustomerReceiptAllocation()
    {
    }

    public CustomerReceiptAllocation(
        Guid customerReceiptId,
        Guid saleId,
        decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Allocation amount must be greater than zero.");
        }

        CustomerReceiptId = customerReceiptId;
        SaleId = saleId;
        Amount = amount;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid CustomerReceiptId { get; private set; }
    public CustomerReceipt CustomerReceipt { get; private set; } = null!;
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = null!;
    public decimal Amount { get; private set; }
}
