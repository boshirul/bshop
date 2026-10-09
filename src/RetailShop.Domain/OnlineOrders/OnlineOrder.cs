using RetailShop.Domain.Common;

namespace RetailShop.Domain.OnlineOrders;

public sealed class OnlineOrder : AuditableEntity
{
    private OnlineOrder()
    {
    }

    public OnlineOrder(
        string orderNumber,
        OnlineOrderSource source,
        string customerName,
        string customerPhone,
        string deliveryAddress,
        decimal deliveryCharge,
        string? notes,
        Guid? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new ArgumentException("Order number is required.", nameof(orderNumber));
        }
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("Customer name is required.", nameof(customerName));
        }
        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            throw new ArgumentException("Customer phone is required.", nameof(customerPhone));
        }
        if (string.IsNullOrWhiteSpace(deliveryAddress))
        {
            throw new ArgumentException("Delivery address is required.", nameof(deliveryAddress));
        }
        if (deliveryCharge < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveryCharge),
                "Delivery charge cannot be negative.");
        }

        OrderNumber = orderNumber.Trim();
        Source = source;
        CustomerName = customerName.Trim();
        CustomerPhone = customerPhone.Trim();
        DeliveryAddress = deliveryAddress.Trim();
        DeliveryCharge = deliveryCharge;
        Notes = Clean(notes);
        CreatedBy = createdBy;
        History.Add(new OnlineOrderHistory(
            Id,
            OnlineOrderHistoryAction.Created,
            createdBy,
            Notes));
    }

    public string OrderNumber { get; private set; } = string.Empty;
    public OnlineOrderSource Source { get; private set; }
    public OnlineOrderStatus Status { get; private set; } = OnlineOrderStatus.Pending;
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerPhone { get; private set; } = string.Empty;
    public string DeliveryAddress { get; private set; } = string.Empty;
    public decimal DeliveryCharge { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal GrandTotal => Subtotal + DeliveryCharge;
    public string? CourierName { get; private set; }
    public string? TrackingNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset OrderedOn { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedOn { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public DateTimeOffset? DeliveredOn { get; private set; }
    public ICollection<OnlineOrderDetail> Details { get; private set; } = [];
    public ICollection<OnlineOrderHistory> History { get; private set; } = [];

    public void AddDetail(
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal costPrice)
    {
        if (Status != OnlineOrderStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending online orders can be changed.");
        }
        if (Details.Any(item => item.ProductId == productId))
        {
            throw new InvalidOperationException(
                "Each product can appear only once in an online order.");
        }

        Details.Add(new OnlineOrderDetail(Id, productId, quantity, unitPrice, costPrice));
        Recalculate();
    }

    public void Confirm(Guid performedBy, string? notes)
    {
        EnsureStatus(OnlineOrderStatus.Pending);
        Status = OnlineOrderStatus.Confirmed;
        ConfirmedOn = DateTimeOffset.UtcNow;
        LastModifiedBy = performedBy;
        History.Add(new OnlineOrderHistory(
            Id,
            OnlineOrderHistoryAction.Confirmed,
            performedBy,
            Clean(notes)));
    }

    public void AssignCourier(
        string? courierName,
        string? trackingNumber,
        Guid performedBy,
        string? notes)
    {
        if (Status != OnlineOrderStatus.Confirmed)
        {
            throw new InvalidOperationException(
                "Courier can be assigned only after confirmation.");
        }

        CourierName = Clean(courierName);
        TrackingNumber = Clean(trackingNumber);
        LastModifiedBy = performedBy;
        History.Add(new OnlineOrderHistory(
            Id,
            OnlineOrderHistoryAction.CourierAssigned,
            performedBy,
            Clean(notes)));
    }

    public void Cancel(Guid performedBy, string reason)
    {
        if (Status is OnlineOrderStatus.Cancelled or OnlineOrderStatus.Delivered)
        {
            throw new InvalidOperationException(
                "This online order can no longer be cancelled.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Cancellation reason is required.",
                nameof(reason));
        }

        Status = OnlineOrderStatus.Cancelled;
        CancelledOn = DateTimeOffset.UtcNow;
        LastModifiedBy = performedBy;
        History.Add(new OnlineOrderHistory(
            Id,
            OnlineOrderHistoryAction.Cancelled,
            performedBy,
            reason.Trim()));
    }

    public void Deliver(Guid performedBy, string? notes)
    {
        EnsureStatus(OnlineOrderStatus.Confirmed);
        Status = OnlineOrderStatus.Delivered;
        DeliveredOn = DateTimeOffset.UtcNow;
        LastModifiedBy = performedBy;
        History.Add(new OnlineOrderHistory(
            Id,
            OnlineOrderHistoryAction.Delivered,
            performedBy,
            Clean(notes)));
    }

    private void Recalculate() => Subtotal = Details.Sum(item => item.LineTotal);

    private void EnsureStatus(OnlineOrderStatus status)
    {
        if (Status != status)
        {
            throw new InvalidOperationException(
                $"Online order must be {status} for this action.");
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
