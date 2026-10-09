namespace RetailShop.Domain.OnlineOrders;

public enum OnlineOrderSource
{
    Website = 1,
    Facebook = 2,
    Phone = 3,
    WhatsApp = 4
}

public enum OnlineOrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3,
    Delivered = 4
}

public enum OnlineOrderHistoryAction
{
    Created = 1,
    Confirmed = 2,
    Cancelled = 3,
    CourierAssigned = 4,
    Delivered = 5
}
