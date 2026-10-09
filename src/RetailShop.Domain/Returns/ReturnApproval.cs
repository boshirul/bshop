namespace RetailShop.Domain.Returns;

public sealed class ReturnApproval
{
    private ReturnApproval()
    {
    }

    public ReturnApproval(
        Guid salesReturnId,
        ReturnApprovalAction action,
        Guid performedBy,
        string? notes)
    {
        SalesReturnId = salesReturnId;
        Action = action;
        PerformedBy = performedBy;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid SalesReturnId { get; private set; }
    public SalesReturn SalesReturn { get; private set; } = null!;
    public ReturnApprovalAction Action { get; private set; }
    public Guid PerformedBy { get; private set; }
    public DateTimeOffset PerformedOn { get; private set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; private set; }
}
