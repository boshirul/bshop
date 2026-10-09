using RetailShop.Domain.Contacts;
using RetailShop.Domain.Settings;

namespace RetailShop.Domain.CustomerAccounts;

public sealed class CustomerReceipt
{
    private CustomerReceipt()
    {
    }

    public CustomerReceipt(
        string receiptNumber,
        Guid customerId,
        Guid paymentMethodId,
        decimal amount,
        DateTimeOffset receivedOn,
        Guid createdBy,
        string? referenceNumber,
        string? notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Receipt amount must be greater than zero.");
        }

        ReceiptNumber = receiptNumber.Trim();
        CustomerId = customerId;
        PaymentMethodId = paymentMethodId;
        Amount = amount;
        ReceivedOn = receivedOn;
        CreatedBy = createdBy;
        ReferenceNumber = Clean(referenceNumber);
        Notes = Clean(notes);
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string ReceiptNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public Guid PaymentMethodId { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public decimal AccountAppliedAmount => Amount - AllocatedAmount;
    public DateTimeOffset ReceivedOn { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;
    public ICollection<CustomerReceiptAllocation> Allocations { get; private set; } = [];

    public void Allocate(Guid saleId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Allocation amount must be greater than zero.");
        }
        if (AllocatedAmount + amount > Amount)
        {
            throw new InvalidOperationException(
                "Receipt allocations cannot exceed the receipt amount.");
        }
        if (Allocations.Any(allocation => allocation.SaleId == saleId))
        {
            throw new InvalidOperationException(
                "A receipt can allocate to each invoice only once.");
        }

        Allocations.Add(new CustomerReceiptAllocation(Id, saleId, amount));
        AllocatedAmount += amount;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
