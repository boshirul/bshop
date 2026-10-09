namespace RetailShop.Domain.Purchases;

public sealed class PurchaseReturn
{
    private PurchaseReturn()
    {
    }

    public PurchaseReturn(
        string returnNumber,
        Guid purchaseId,
        DateTimeOffset returnDate,
        string reason,
        Guid createdBy,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A return reason is required.", nameof(reason));
        }

        ReturnNumber = returnNumber.Trim();
        PurchaseId = purchaseId;
        ReturnDate = returnDate;
        Reason = reason.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedBy = createdBy;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string ReturnNumber { get; private set; } = string.Empty;
    public Guid PurchaseId { get; private set; }
    public Purchase Purchase { get; private set; } = null!;
    public DateTimeOffset ReturnDate { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public decimal TotalAmount { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; } = DateTimeOffset.UtcNow;
    public ICollection<PurchaseReturnDetail> Details { get; private set; } = [];

    public void AddDetail(Guid purchaseDetailId, decimal quantity, decimal amount)
    {
        Details.Add(new PurchaseReturnDetail(Id, purchaseDetailId, quantity, amount));
        TotalAmount += amount;
    }
}
