using RetailShop.Domain.Settings;

namespace RetailShop.Domain.Purchases;

public sealed class PurchasePayment
{
    private PurchasePayment()
    {
    }

    public PurchasePayment(
        Guid purchaseId,
        Guid paymentMethodId,
        decimal amount,
        DateTimeOffset paidOn,
        Guid createdBy,
        string? referenceNumber,
        string? notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be greater than zero.");
        }

        PurchaseId = purchaseId;
        PaymentMethodId = paymentMethodId;
        Amount = amount;
        PaidOn = paidOn;
        CreatedBy = createdBy;
        ReferenceNumber = Clean(referenceNumber);
        Notes = Clean(notes);
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid PurchaseId { get; private set; }
    public Purchase Purchase { get; private set; } = null!;
    public Guid PaymentMethodId { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public DateTimeOffset PaidOn { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
