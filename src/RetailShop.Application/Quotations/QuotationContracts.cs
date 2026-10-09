using RetailShop.Application.Common;
using RetailShop.Application.Sales;
using RetailShop.Domain.Quotations;

namespace RetailShop.Application.Quotations;

public sealed record SaveQuotationRequest(
    Guid CustomerId,
    DateTimeOffset QuotationDate,
    DateTimeOffset ValidUntil,
    string? Notes,
    string? Terms,
    IReadOnlyCollection<SaveQuotationItemRequest> Items);

public sealed record SaveQuotationItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal VatAmount);

public sealed record RejectQuotationRequest(string Reason);

public sealed record ConvertQuotationRequest(
    DateTimeOffset SaleDate,
    string? Notes,
    IReadOnlyCollection<CreateSalePaymentRequest> Payments);

public sealed record QuotationListItem(
    Guid Id,
    string QuotationNumber,
    Guid CustomerId,
    string CustomerName,
    DateTimeOffset QuotationDate,
    DateTimeOffset ValidUntil,
    QuotationStatus Status,
    decimal GrandTotal,
    Guid? ConvertedSaleId);

public sealed record QuotationDetailItem(
    Guid Id,
    string QuotationNumber,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    DateTimeOffset QuotationDate,
    DateTimeOffset ValidUntil,
    QuotationStatus Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal GrandTotal,
    string? Notes,
    string? Terms,
    string? RejectionReason,
    DateTimeOffset? SentOn,
    DateTimeOffset? AcceptedOn,
    DateTimeOffset? RejectedOn,
    DateTimeOffset? ConvertedOn,
    Guid? ConvertedSaleId,
    IReadOnlyCollection<QuotationLineItem> Items);

public sealed record QuotationLineItem(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal LineTotal);

public interface IQuotationService
{
    Task<PagedResult<QuotationListItem>> GetAsync(
        string? search,
        QuotationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<QuotationDetailItem>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<QuotationDetailItem>> CreateAsync(
        SaveQuotationRequest request,
        Guid performedBy,
        bool canChangePrice,
        bool canDiscount,
        CancellationToken cancellationToken);

    Task<OperationResult<QuotationDetailItem>> UpdateAsync(
        Guid id,
        SaveQuotationRequest request,
        Guid performedBy,
        bool canChangePrice,
        bool canDiscount,
        CancellationToken cancellationToken);

    Task<OperationResult<QuotationDetailItem>> SendAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<QuotationDetailItem>> AcceptAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<QuotationDetailItem>> RejectAsync(
        Guid id,
        RejectQuotationRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<SaleDetailItem>> ConvertAsync(
        Guid id,
        ConvertQuotationRequest request,
        Guid performedBy,
        bool canSellOnDue,
        CancellationToken cancellationToken);
}
