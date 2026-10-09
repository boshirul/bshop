using RetailShop.Application.Common;
using RetailShop.Domain.Purchases;

namespace RetailShop.Application.Purchases;

public sealed record CreatePurchaseRequest(
    Guid SupplierId,
    string? SupplierInvoiceNumber,
    DateTimeOffset PurchaseDate,
    string? Notes,
    IReadOnlyCollection<CreatePurchaseItemRequest> Items,
    IReadOnlyCollection<CreatePurchasePaymentRequest> Payments);

public sealed record CreatePurchaseItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal VatAmount);

public sealed record CreatePurchasePaymentRequest(
    Guid PaymentMethodId,
    decimal Amount,
    string? ReferenceNumber,
    string? Notes);

public sealed record RecordPurchasePaymentRequest(
    Guid PaymentMethodId,
    decimal Amount,
    DateTimeOffset PaidOn,
    string? ReferenceNumber,
    string? Notes);

public sealed record CreatePurchaseReturnRequest(
    DateTimeOffset ReturnDate,
    string Reason,
    string? Notes,
    IReadOnlyCollection<CreatePurchaseReturnItemRequest> Items);

public sealed record CreatePurchaseReturnItemRequest(
    Guid PurchaseDetailId,
    decimal Quantity);

public sealed record PurchaseListItem(
    Guid Id,
    string PurchaseNumber,
    Guid SupplierId,
    string SupplierName,
    string? SupplierInvoiceNumber,
    DateTimeOffset PurchaseDate,
    PurchaseStatus Status,
    PurchasePaymentStatus PaymentStatus,
    decimal GrandTotal,
    decimal ReturnedAmount,
    decimal PaidAmount,
    decimal DueAmount);

public sealed record PurchaseDetailItem(
    Guid Id,
    string PurchaseNumber,
    Guid SupplierId,
    string SupplierName,
    string? SupplierInvoiceNumber,
    DateTimeOffset PurchaseDate,
    PurchaseStatus Status,
    PurchasePaymentStatus PaymentStatus,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal GrandTotal,
    decimal ReturnedAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string? Notes,
    IReadOnlyCollection<PurchaseLineItem> Items,
    IReadOnlyCollection<PurchasePaymentItem> Payments,
    IReadOnlyCollection<PurchaseReturnItem> Returns);

public sealed record PurchaseLineItem(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal UnitCost,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal LineTotal);

public sealed record PurchasePaymentItem(
    Guid Id,
    Guid PaymentMethodId,
    string PaymentMethodName,
    decimal Amount,
    DateTimeOffset PaidOn,
    string? ReferenceNumber,
    string? Notes);

public sealed record PurchaseReturnItem(
    Guid Id,
    string ReturnNumber,
    DateTimeOffset ReturnDate,
    string Reason,
    decimal TotalAmount,
    string? Notes);

public sealed record SupplierLedgerItem(
    Guid Id,
    DateTimeOffset EntryDate,
    SupplierLedgerEntryType EntryType,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string ReferenceType,
    Guid ReferenceId,
    string ReferenceNumber,
    string? Notes);

public sealed record SupplierLedgerResult(
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    decimal CurrentBalance,
    PagedResult<SupplierLedgerItem> Entries);

public interface IPurchaseService
{
    Task<PagedResult<PurchaseListItem>> GetPurchasesAsync(
        string? search,
        Guid? supplierId,
        PurchaseStatus? status,
        PurchasePaymentStatus? paymentStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<PurchaseDetailItem>> GetPurchaseAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<PurchaseDetailItem>> CreatePurchaseAsync(
        CreatePurchaseRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<PurchaseDetailItem>> RecordPaymentAsync(
        Guid purchaseId,
        RecordPurchasePaymentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<PurchaseDetailItem>> CreateReturnAsync(
        Guid purchaseId,
        CreatePurchaseReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<SupplierLedgerResult>> GetSupplierLedgerAsync(
        Guid supplierId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
