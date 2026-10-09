namespace RetailShop.Domain.OnlineOrders;

public sealed class OnlineOrderHistory
{
    private OnlineOrderHistory()
    {
    }

    public OnlineOrderHistory(
        Guid onlineOrderId,
        OnlineOrderHistoryAction action,
        Guid? performedBy,
        string? notes)
    {
        OnlineOrderId = onlineOrderId;
        Action = action;
        PerformedBy = performedBy;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid OnlineOrderId { get; private set; }
    public OnlineOrder OnlineOrder { get; private set; } = null!;
    public OnlineOrderHistoryAction Action { get; private set; }
    public Guid? PerformedBy { get; private set; }
    public DateTimeOffset PerformedOn { get; private set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; private set; }
}
