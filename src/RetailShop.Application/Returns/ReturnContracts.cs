using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;
using RetailShop.Domain.Returns;

namespace RetailShop.Application.Returns;

public sealed record ComplaintReasonItem(Guid Id, string Name);

public sealed record ReturnInvoiceLineItem(
    Guid SaleDetailId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string Unit,
    decimal SoldQuantity,
    decimal ReturnedQuantity,
    decimal ReturnableQuantity,
    decimal UnitReturnAmount);

public sealed record ReturnInvoiceItem(
    Guid SaleId,
    string InvoiceNumber,
    Guid? CustomerId,
    string CustomerName,
    DateTimeOffset SaleDate,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal DueAmount,
    IReadOnlyCollection<ReturnInvoiceLineItem> Lines);

public sealed record CreateSalesReturnLineRequest(
    Guid SaleDetailId,
    decimal Quantity);

public sealed record CreateSalesReturnRequest(
    [param: Required] string InvoiceNumber,
    Guid ComplaintReasonId,
    ReturnProductCondition ProductCondition,
    SalesReturnAction RequestedAction,
    [param: MinLength(1)] IReadOnlyCollection<CreateSalesReturnLineRequest> Items,
    string? Notes);

public sealed record ReviewSalesReturnRequest(
    Guid? PaymentMethodId,
    string? Notes);

public sealed record RejectSalesReturnRequest(
    [param: Required, MinLength(3)] string Notes);

public sealed record SalesReturnListItem(
    Guid Id,
    string ReturnNumber,
    string InvoiceNumber,
    string CustomerName,
    string ComplaintReason,
    SalesReturnAction RequestedAction,
    ReturnProductCondition ProductCondition,
    SalesReturnStatus Status,
    decimal TotalAmount,
    DateTimeOffset RequestedOn);

public sealed record SalesReturnLineItem(
    Guid Id,
    Guid SaleDetailId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal Amount);

public sealed record ReturnApprovalItem(
    ReturnApprovalAction Action,
    Guid PerformedBy,
    DateTimeOffset PerformedOn,
    string? Notes);

public sealed record SalesReturnDetailItem(
    Guid Id,
    string ReturnNumber,
    Guid SaleId,
    string InvoiceNumber,
    Guid? CustomerId,
    string CustomerName,
    string ComplaintReason,
    ReturnProductCondition ProductCondition,
    SalesReturnAction RequestedAction,
    SalesReturnStatus Status,
    decimal TotalAmount,
    decimal RefundedAmount,
    decimal DueAdjustedAmount,
    string? PaymentMethodName,
    string? Notes,
    string? ReviewNotes,
    Guid RequestedBy,
    DateTimeOffset RequestedOn,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedOn,
    IReadOnlyCollection<SalesReturnLineItem> Items,
    IReadOnlyCollection<ReturnApprovalItem> History);

public interface IReturnService
{
    Task<IReadOnlyCollection<ComplaintReasonItem>> GetComplaintReasonsAsync(
        CancellationToken cancellationToken);

    Task<OperationResult<ReturnInvoiceItem>> GetInvoiceAsync(
        string invoiceNumber,
        CancellationToken cancellationToken);

    Task<PagedResult<SalesReturnListItem>> GetReturnsAsync(
        string? search,
        SalesReturnStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<SalesReturnDetailItem>> GetReturnAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<SalesReturnDetailItem>> CreateReturnAsync(
        CreateSalesReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<SalesReturnDetailItem>> ApproveAsync(
        Guid id,
        ReviewSalesReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<SalesReturnDetailItem>> RejectAsync(
        Guid id,
        RejectSalesReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
}
