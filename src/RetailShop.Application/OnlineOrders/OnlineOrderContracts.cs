using RetailShop.Application.Common;
using RetailShop.Domain.OnlineOrders;

namespace RetailShop.Application.OnlineOrders;

public sealed record OnlineProductListItem(
    Guid Id,
    string ProductCode,
    string Name,
    string? BrandName,
    string UnitSymbol,
    decimal SalePrice,
    string? ImageUrl,
    decimal AvailableQuantity);

public sealed record OnlineProductDetailItem(
    Guid Id,
    string ProductCode,
    string Name,
    string? BrandName,
    string UnitSymbol,
    string? Description,
    decimal SalePrice,
    string? ImageUrl,
    decimal AvailableQuantity,
    bool IsWarrantyAvailable,
    int? WarrantyMonths);

public sealed record CreateOnlineOrderRequest(
    OnlineOrderSource Source,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    decimal DeliveryCharge,
    string? Notes,
    IReadOnlyCollection<CreateOnlineOrderLineRequest> Items);

public sealed record CreateOnlineOrderLineRequest(
    Guid ProductId,
    decimal Quantity);

public sealed record OnlineOrderListItem(
    Guid Id,
    string OrderNumber,
    OnlineOrderSource Source,
    OnlineOrderStatus Status,
    string CustomerName,
    string CustomerPhone,
    DateTimeOffset OrderedOn,
    decimal GrandTotal,
    string? CourierName,
    string? TrackingNumber);

public sealed record OnlineOrderDetailItem(
    Guid Id,
    string OrderNumber,
    OnlineOrderSource Source,
    OnlineOrderStatus Status,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    decimal Subtotal,
    decimal DeliveryCharge,
    decimal GrandTotal,
    string? CourierName,
    string? TrackingNumber,
    string? Notes,
    DateTimeOffset OrderedOn,
    DateTimeOffset? ConfirmedOn,
    DateTimeOffset? CancelledOn,
    DateTimeOffset? DeliveredOn,
    IReadOnlyCollection<OnlineOrderLineItem> Items,
    IReadOnlyCollection<OnlineOrderHistoryItem> History);

public sealed record OnlineOrderLineItem(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OnlineOrderHistoryItem(
    Guid Id,
    OnlineOrderHistoryAction Action,
    Guid? PerformedBy,
    DateTimeOffset PerformedOn,
    string? Notes);

public sealed record ConfirmOnlineOrderRequest(string? Notes);

public sealed record AssignCourierRequest(
    string? CourierName,
    string? TrackingNumber,
    string? Notes);

public sealed record CancelOnlineOrderRequest(string Reason);

public sealed record DeliverOnlineOrderRequest(string? Notes);

public interface IOnlineOrderService
{
    Task<PagedResult<OnlineProductListItem>> GetStoreProductsAsync(
        string? search,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineProductDetailItem>> GetStoreProductAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineOrderDetailItem>> CreateOrderAsync(
        CreateOnlineOrderRequest request,
        Guid? performedBy,
        CancellationToken cancellationToken);

    Task<PagedResult<OnlineOrderListItem>> GetOrdersAsync(
        string? search,
        OnlineOrderStatus? status,
        OnlineOrderSource? source,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineOrderDetailItem>> GetOrderAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineOrderDetailItem>> ConfirmAsync(
        Guid id,
        ConfirmOnlineOrderRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineOrderDetailItem>> AssignCourierAsync(
        Guid id,
        AssignCourierRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineOrderDetailItem>> CancelAsync(
        Guid id,
        CancelOnlineOrderRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<OnlineOrderDetailItem>> DeliverAsync(
        Guid id,
        DeliverOnlineOrderRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
}
