namespace RetailShop.Domain.Warranty;

public sealed class WarrantyClaimHistory
{
    private WarrantyClaimHistory()
    {
    }

    public WarrantyClaimHistory(
        Guid warrantyClaimId,
        WarrantyClaimHistoryAction action,
        Guid performedBy,
        string? notes)
    {
        WarrantyClaimId = warrantyClaimId;
        Action = action;
        PerformedBy = performedBy;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid WarrantyClaimId { get; private set; }
    public WarrantyClaim WarrantyClaim { get; private set; } = null!;
    public WarrantyClaimHistoryAction Action { get; private set; }
    public Guid PerformedBy { get; private set; }
    public DateTimeOffset PerformedOn { get; private set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; private set; }
}
