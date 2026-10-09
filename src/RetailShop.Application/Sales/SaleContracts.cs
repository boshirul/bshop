using RetailShop.Application.Common;
using RetailShop.Domain.Sales;

namespace RetailShop.Application.Sales;

public sealed record CreateSaleRequest(
    Guid? CustomerId,
    DateTimeOffset SaleDate,
    string? Notes,
    IReadOnlyCollection<CreateSaleItemRequest> Items,
    IReadOnlyCollection<CreateSalePaymentRequest> Payments);

public sealed record CreateSaleItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal VatAmount,
    IReadOnlyCollection<string>? SerialNumbers = null);

public sealed record CreateSalePaymentRequest(
    Guid PaymentMethodId,
    decimal Amount,
    string? ReferenceNumber,
    string? Notes);

public sealed record RecordSalePaymentRequest(
    Guid PaymentMethodId,
    decimal Amount,
    DateTimeOffset PaidOn,
    string? ReferenceNumber,
    string? Notes);

public sealed record CancelSaleRequest(string Reason);

public sealed record SaleListItem(
    Guid Id,
    string InvoiceNumber,
    Guid? CustomerId,
    string CustomerName,
    DateTimeOffset SaleDate,
    SaleStatus Status,
    SalePaymentStatus PaymentStatus,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal DueAmount);

public sealed record SaleDetailItem(
    Guid Id,
    string InvoiceNumber,
    Guid? CustomerId,
    string CustomerName,
    string? CustomerPhone,
    DateTimeOffset SaleDate,
    SaleStatus Status,
    SalePaymentStatus PaymentStatus,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal DueAmount,
    decimal Profit,
    string? Notes,
    string? CancellationReason,
    DateTimeOffset? CancelledOn,
    IReadOnlyCollection<SaleLineItem> Items,
    IReadOnlyCollection<SalePaymentItem> Payments);

public sealed record SaleLineItem(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal LineTotal,
    decimal CostPrice,
    decimal Profit);

public sealed record SalePaymentItem(
    Guid Id,
    Guid PaymentMethodId,
    string PaymentMethodName,
    decimal Amount,
    DateTimeOffset PaidOn,
    string? ReferenceNumber,
    string? Notes);

public sealed record CustomerLedgerItem(
    Guid Id,
    DateTimeOffset EntryDate,
    CustomerLedgerEntryType EntryType,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string ReferenceType,
    Guid ReferenceId,
    string ReferenceNumber,
    string? Notes);

public sealed record CustomerLedgerResult(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    decimal CreditLimit,
    decimal CurrentBalance,
    PagedResult<CustomerLedgerItem> Entries);

public interface ISaleService
{
    Task<PagedResult<SaleListItem>> GetSalesAsync(
        string? search,
        Guid? customerId,
        SaleStatus? status,
        SalePaymentStatus? paymentStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<SaleDetailItem>> GetSaleAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<SaleDetailItem>> CreateSaleAsync(
        CreateSaleRequest request,
        Guid performedBy,
        bool canChangePrice,
        bool canDiscount,
        bool canSellOnDue,
        CancellationToken cancellationToken);

    Task<OperationResult<SaleDetailItem>> RecordPaymentAsync(
        Guid saleId,
        RecordSalePaymentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<SaleDetailItem>> CancelSaleAsync(
        Guid saleId,
        CancelSaleRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<CustomerLedgerResult>> GetCustomerLedgerAsync(
        Guid customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
