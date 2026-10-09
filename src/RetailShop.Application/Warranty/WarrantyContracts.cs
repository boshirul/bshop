using RetailShop.Application.Common;
using RetailShop.Domain.Warranty;

namespace RetailShop.Application.Warranty;

public sealed record RegisterProductSerialsRequest(
    Guid ProductId,
    Guid? PurchaseId,
    Guid? PurchaseDetailId,
    IReadOnlyCollection<string> SerialNumbers);

public sealed record ProductSerialItem(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string SerialNumber,
    ProductSerialStatus Status,
    Guid? SaleId,
    string? InvoiceNumber,
    string? CustomerName,
    DateOnly? WarrantyStartDate,
    DateOnly? WarrantyExpiryDate,
    bool WarrantyActive);

public sealed record WarrantyLookupItem(
    ProductSerialItem Serial,
    IReadOnlyCollection<WarrantyClaimListItem> Claims);

public sealed record CreateWarrantyClaimRequest(
    string SerialNumber,
    string Complaint,
    WarrantyResolutionAction RequestedAction,
    string? Notes);

public sealed record ReviewWarrantyClaimRequest(string? Notes);

public sealed record RejectWarrantyClaimRequest(string Reason);

public sealed record ResolveWarrantyClaimRequest(
    WarrantyResolutionAction Action,
    string? ReplacementSerialNumber,
    string? Notes);

public sealed record WarrantyClaimListItem(
    Guid Id,
    string ClaimNumber,
    string SerialNumber,
    string ProductCode,
    string ProductName,
    string? InvoiceNumber,
    string? CustomerName,
    WarrantyClaimStatus Status,
    WarrantyResolutionAction RequestedAction,
    DateTimeOffset RequestedOn);

public sealed record WarrantyClaimDetailItem(
    Guid Id,
    string ClaimNumber,
    ProductSerialItem Serial,
    string? InvoiceNumber,
    string? CustomerName,
    string Complaint,
    WarrantyResolutionAction RequestedAction,
    WarrantyClaimStatus Status,
    DateTimeOffset RequestedOn,
    DateTimeOffset? ReviewedOn,
    DateTimeOffset? ResolvedOn,
    string? Notes,
    string? ReviewNotes,
    IReadOnlyCollection<WarrantyClaimHistoryItem> History);

public sealed record WarrantyClaimHistoryItem(
    Guid Id,
    WarrantyClaimHistoryAction Action,
    Guid PerformedBy,
    DateTimeOffset PerformedOn,
    string? Notes);

public interface IWarrantyService
{
    Task<IReadOnlyCollection<ProductSerialItem>> RegisterSerialsAsync(
        RegisterProductSerialsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProductSerialItem>> GetAvailableSerialsAsync(
        Guid productId,
        CancellationToken cancellationToken);

    Task<OperationResult<WarrantyLookupItem>> LookupBySerialAsync(
        string serialNumber,
        CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyCollection<WarrantyLookupItem>>> LookupByInvoiceAsync(
        string invoiceNumber,
        CancellationToken cancellationToken);

    Task<PagedResult<WarrantyClaimListItem>> GetClaimsAsync(
        string? search,
        WarrantyClaimStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<WarrantyClaimDetailItem>> GetClaimAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<OperationResult<WarrantyClaimDetailItem>> CreateClaimAsync(
        CreateWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<WarrantyClaimDetailItem>> ApproveAsync(
        Guid id,
        ReviewWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<WarrantyClaimDetailItem>> RejectAsync(
        Guid id,
        RejectWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);

    Task<OperationResult<WarrantyClaimDetailItem>> ResolveAsync(
        Guid id,
        ResolveWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
}
