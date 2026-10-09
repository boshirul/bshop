using RetailShop.Application.Common;
using RetailShop.Domain.Sales;

namespace RetailShop.Application.CustomerAccounts;

public sealed record CreateCustomerReceiptRequest(
    Guid CustomerId,
    Guid PaymentMethodId,
    decimal Amount,
    DateTimeOffset ReceivedOn,
    string? ReferenceNumber,
    string? Notes);

public sealed record CustomerReceiptListItem(
    Guid Id,
    string ReceiptNumber,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string PaymentMethodName,
    decimal Amount,
    decimal AllocatedAmount,
    decimal AccountAppliedAmount,
    DateTimeOffset ReceivedOn,
    string? ReferenceNumber);

public sealed record CustomerReceiptDetailItem(
    Guid Id,
    string ReceiptNumber,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string CustomerPhone,
    string? CustomerAddress,
    Guid PaymentMethodId,
    string PaymentMethodName,
    decimal Amount,
    decimal AllocatedAmount,
    decimal AccountAppliedAmount,
    DateTimeOffset ReceivedOn,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyCollection<CustomerReceiptAllocationItem> Allocations);

public sealed record CustomerReceiptAllocationItem(
    Guid Id,
    Guid SaleId,
    string InvoiceNumber,
    DateTimeOffset SaleDate,
    decimal Amount);

public sealed record CustomerDueSummaryItem(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string Phone,
    decimal CreditLimit,
    decimal CurrentBalance,
    decimal InvoiceDue,
    int OutstandingInvoiceCount,
    DateTimeOffset? OldestInvoiceDate);

public sealed record CustomerAgingItem(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);

public sealed record CustomerAgingResult(
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total,
    IReadOnlyCollection<CustomerAgingItem> Customers);

public sealed record CustomerStatementEntry(
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

public sealed record CustomerStatementResult(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string Phone,
    string? Address,
    DateTimeOffset? From,
    DateTimeOffset? To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyCollection<CustomerStatementEntry> Entries);

public enum CustomerAdjustmentDirection
{
    Debit,
    Credit
}

public sealed record CreateCustomerAdjustmentRequest(
    Guid CustomerId,
    DateTimeOffset AdjustmentDate,
    CustomerAdjustmentDirection Direction,
    decimal Amount,
    string Reason);

public interface ICustomerAccountService
{
    Task<PagedResult<CustomerDueSummaryItem>> GetDuesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CustomerAgingResult> GetAgingAsync(
        CancellationToken cancellationToken);

    Task<PagedResult<CustomerReceiptListItem>> GetReceiptsAsync(
        string? search,
        Guid? customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<CustomerReceiptDetailItem>> GetReceiptAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<CustomerReceiptDetailItem>> CreateReceiptAsync(
        CreateCustomerReceiptRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<CustomerStatementResult>> GetStatementAsync(
        Guid customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken);

    Task<OperationResult<CustomerStatementResult>> CreateAdjustmentAsync(
        CreateCustomerAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
}
