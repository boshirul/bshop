using RetailShop.Domain.Common;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Sales;

namespace RetailShop.Domain.Warranty;

public sealed class WarrantyClaim : AuditableEntity
{
    private WarrantyClaim()
    {
    }

    public WarrantyClaim(
        string claimNumber,
        Guid productSerialId,
        Guid saleId,
        Guid? customerId,
        string complaint,
        WarrantyResolutionAction requestedAction,
        Guid requestedBy,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            throw new ArgumentException("Claim number is required.", nameof(claimNumber));
        }
        if (string.IsNullOrWhiteSpace(complaint))
        {
            throw new ArgumentException("Complaint is required.", nameof(complaint));
        }

        ClaimNumber = claimNumber.Trim();
        ProductSerialId = productSerialId;
        SaleId = saleId;
        CustomerId = customerId;
        Complaint = complaint.Trim();
        RequestedAction = requestedAction;
        RequestedBy = requestedBy;
        RequestedOn = DateTimeOffset.UtcNow;
        Notes = Clean(notes);
        Status = WarrantyClaimStatus.Pending;
        CreatedBy = requestedBy;
        History.Add(new WarrantyClaimHistory(
            Id,
            WarrantyClaimHistoryAction.Submitted,
            requestedBy,
            Notes));
    }

    public string ClaimNumber { get; private set; } = string.Empty;
    public Guid ProductSerialId { get; private set; }
    public ProductSerial ProductSerial { get; private set; } = null!;
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = null!;
    public Guid? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public string Complaint { get; private set; } = string.Empty;
    public WarrantyResolutionAction RequestedAction { get; private set; }
    public WarrantyClaimStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedOn { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedOn { get; private set; }
    public string? Notes { get; private set; }
    public string? ReviewNotes { get; private set; }
    public ICollection<WarrantyClaimHistory> History { get; private set; } = [];

    public void Approve(Guid performedBy, string? notes)
    {
        EnsurePending();
        Status = WarrantyClaimStatus.Approved;
        ReviewedBy = performedBy;
        ReviewedOn = DateTimeOffset.UtcNow;
        ReviewNotes = Clean(notes);
        LastModifiedBy = performedBy;
        History.Add(new WarrantyClaimHistory(
            Id,
            WarrantyClaimHistoryAction.Approved,
            performedBy,
            ReviewNotes));
    }

    public void Reject(Guid performedBy, string reason)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A rejection reason is required.", nameof(reason));
        }

        Status = WarrantyClaimStatus.Rejected;
        ReviewedBy = performedBy;
        ReviewedOn = DateTimeOffset.UtcNow;
        ReviewNotes = reason.Trim();
        LastModifiedBy = performedBy;
        History.Add(new WarrantyClaimHistory(
            Id,
            WarrantyClaimHistoryAction.Rejected,
            performedBy,
            ReviewNotes));
    }

    public void Resolve(
        WarrantyResolutionAction action,
        Guid performedBy,
        string? notes)
    {
        if (Status is WarrantyClaimStatus.Rejected or WarrantyClaimStatus.Resolved)
        {
            throw new InvalidOperationException(
                "This warranty claim cannot be resolved.");
        }

        Status = action switch
        {
            WarrantyResolutionAction.SupplierClaim => WarrantyClaimStatus.SupplierClaim,
            WarrantyResolutionAction.Repair => WarrantyClaimStatus.Repaired,
            WarrantyResolutionAction.Replacement => WarrantyClaimStatus.Replaced,
            WarrantyResolutionAction.Refund => WarrantyClaimStatus.Refunded,
            WarrantyResolutionAction.Resolve => WarrantyClaimStatus.Resolved,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        ResolvedBy = performedBy;
        ResolvedOn = DateTimeOffset.UtcNow;
        LastModifiedBy = performedBy;
        History.Add(new WarrantyClaimHistory(
            Id,
            ToHistoryAction(Status),
            performedBy,
            Clean(notes)));
    }

    private void EnsurePending()
    {
        if (Status != WarrantyClaimStatus.Pending)
        {
            throw new InvalidOperationException("Only pending warranty claims can be reviewed.");
        }
    }

    private static WarrantyClaimHistoryAction ToHistoryAction(
        WarrantyClaimStatus status) =>
        status switch
        {
            WarrantyClaimStatus.SupplierClaim => WarrantyClaimHistoryAction.SupplierClaim,
            WarrantyClaimStatus.Repaired => WarrantyClaimHistoryAction.Repaired,
            WarrantyClaimStatus.Replaced => WarrantyClaimHistoryAction.Replaced,
            WarrantyClaimStatus.Refunded => WarrantyClaimHistoryAction.Refunded,
            _ => WarrantyClaimHistoryAction.Resolved
        };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
