using RetailShop.Domain.Sales;
using RetailShop.Domain.Settings;

namespace RetailShop.Domain.Returns;

public sealed class SalesReturn
{
    private SalesReturn()
    {
    }

    public SalesReturn(
        string returnNumber,
        Guid saleId,
        Guid complaintReasonId,
        ReturnProductCondition productCondition,
        SalesReturnAction requestedAction,
        Guid requestedBy,
        string? notes)
    {
        ReturnNumber = string.IsNullOrWhiteSpace(returnNumber)
            ? throw new ArgumentException("A return number is required.", nameof(returnNumber))
            : returnNumber.Trim();
        SaleId = saleId;
        ComplaintReasonId = complaintReasonId;
        ProductCondition = productCondition;
        RequestedAction = requestedAction;
        RequestedBy = requestedBy;
        Notes = Clean(notes);
        History.Add(new ReturnApproval(
            Id,
            ReturnApprovalAction.Submitted,
            requestedBy,
            "Return request submitted."));
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public string ReturnNumber { get; private set; } = string.Empty;
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = null!;
    public Guid ComplaintReasonId { get; private set; }
    public ComplaintReason ComplaintReason { get; private set; } = null!;
    public ReturnProductCondition ProductCondition { get; private set; }
    public SalesReturnAction RequestedAction { get; private set; }
    public SalesReturnStatus Status { get; private set; } = SalesReturnStatus.Pending;
    public decimal TotalAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public decimal DueAdjustedAmount { get; private set; }
    public Guid? PaymentMethodId { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }
    public string? Notes { get; private set; }
    public string? ReviewNotes { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedOn { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedOn { get; private set; }
    public ICollection<SalesReturnDetail> Details { get; private set; } = [];
    public ICollection<ReturnApproval> History { get; private set; } = [];

    public void AddDetail(Guid saleDetailId, decimal quantity, decimal amount)
    {
        if (Status != SalesReturnStatus.Pending)
        {
            throw new InvalidOperationException("Only pending returns can be changed.");
        }
        if (Details.Any(item => item.SaleDetailId == saleDetailId))
        {
            throw new InvalidOperationException("Each sold item can appear only once.");
        }

        Details.Add(new SalesReturnDetail(Id, saleDetailId, quantity, amount));
        TotalAmount += amount;
    }

    public void Resolve(
        Guid reviewedBy,
        decimal refundedAmount,
        decimal dueAdjustedAmount,
        Guid? paymentMethodId,
        string? notes)
    {
        EnsurePending();
        if (refundedAmount < 0 || dueAdjustedAmount < 0 ||
            refundedAmount + dueAdjustedAmount > TotalAmount)
        {
            throw new InvalidOperationException("Return resolution amounts are invalid.");
        }

        Status = RequestedAction switch
        {
            SalesReturnAction.Refund => SalesReturnStatus.Refunded,
            SalesReturnAction.Replacement => SalesReturnStatus.Replaced,
            SalesReturnAction.DueAdjustment => SalesReturnStatus.Adjusted,
            _ => throw new ArgumentOutOfRangeException()
        };
        RefundedAmount = refundedAmount;
        DueAdjustedAmount = dueAdjustedAmount;
        PaymentMethodId = paymentMethodId;
        ReviewedBy = reviewedBy;
        ReviewedOn = DateTimeOffset.UtcNow;
        ReviewNotes = Clean(notes);
        History.Add(new ReturnApproval(
            Id,
            ReturnApprovalAction.Approved,
            reviewedBy,
            ReviewNotes));
    }

    public void Reject(Guid reviewedBy, string notes)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException("A rejection reason is required.", nameof(notes));
        }

        Status = SalesReturnStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedOn = DateTimeOffset.UtcNow;
        ReviewNotes = notes.Trim();
        History.Add(new ReturnApproval(
            Id,
            ReturnApprovalAction.Rejected,
            reviewedBy,
            ReviewNotes));
    }

    private void EnsurePending()
    {
        if (Status != SalesReturnStatus.Pending)
        {
            throw new InvalidOperationException("The return has already been reviewed.");
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
