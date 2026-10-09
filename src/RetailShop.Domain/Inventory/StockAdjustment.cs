namespace RetailShop.Domain.Inventory;

public sealed class StockAdjustment
{
    private StockAdjustment()
    {
    }

    public StockAdjustment(string adjustmentNumber, string reason, Guid requestedBy)
    {
        AdjustmentNumber = adjustmentNumber.Trim();
        Reason = reason.Trim();
        RequestedBy = requestedBy;
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public string AdjustmentNumber { get; private set; } = string.Empty;

    public DateTimeOffset RequestedOn { get; private set; } = DateTimeOffset.UtcNow;

    public string Reason { get; private set; } = string.Empty;

    public string? Notes { get; set; }

    public StockAdjustmentStatus Status { get; private set; } =
        StockAdjustmentStatus.Pending;

    public Guid RequestedBy { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedOn { get; private set; }

    public string? ReviewNotes { get; private set; }

    public Guid? ReversedBy { get; private set; }

    public DateTimeOffset? ReversedOn { get; private set; }

    public string? ReversalReason { get; private set; }

    public ICollection<StockAdjustmentDetail> Details { get; private set; } = [];

    public void Approve(Guid reviewedBy, string? reviewNotes)
    {
        EnsurePending();
        Status = StockAdjustmentStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedOn = DateTimeOffset.UtcNow;
        ReviewNotes = Clean(reviewNotes);
    }

    public void Reject(Guid reviewedBy, string reviewNotes)
    {
        EnsurePending();
        Status = StockAdjustmentStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedOn = DateTimeOffset.UtcNow;
        ReviewNotes = Clean(reviewNotes);
    }

    public void Reverse(Guid reversedBy, string reason)
    {
        if (Status != StockAdjustmentStatus.Approved)
        {
            throw new InvalidOperationException(
                "Only an approved adjustment can be reversed.");
        }

        Status = StockAdjustmentStatus.Reversed;
        ReversedBy = reversedBy;
        ReversedOn = DateTimeOffset.UtcNow;
        ReversalReason = reason.Trim();
    }

    private void EnsurePending()
    {
        if (Status != StockAdjustmentStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending adjustment can be reviewed.");
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
