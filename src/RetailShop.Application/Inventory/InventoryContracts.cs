using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;
using RetailShop.Domain.Inventory;

namespace RetailShop.Application.Inventory;

public sealed record OpeningStockItemRequest(
    Guid ProductId,
    [param: Range(typeof(decimal), "0.001", "999999999")] decimal Quantity,
    [param: Range(typeof(decimal), "0", "999999999")] decimal UnitCost);

public sealed record OpeningStockRequest(
    [param: Required, MinLength(1)] IReadOnlyCollection<OpeningStockItemRequest> Items,
    [param: MaxLength(500)] string? Remarks);

public sealed record OpeningStockResult(
    Guid BatchId,
    DateTimeOffset RecordedOn,
    int ProductCount);

public sealed record CurrentStockItem(
    Guid ProductId,
    string ProductCode,
    string Barcode,
    string ProductName,
    string CategoryName,
    string UnitSymbol,
    decimal AvailableQuantity,
    decimal ReservedQuantity,
    decimal DamagedQuantity,
    decimal WarrantyQuantity,
    decimal SupplierClaimQuantity,
    decimal TotalQuantity,
    decimal MinimumStockLevel,
    decimal AverageCost,
    decimal StockValue,
    bool IsLowStock);

public sealed record StockLedgerItem(
    Guid Id,
    DateTimeOffset TransactionDate,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    StockTransactionType TransactionType,
    StockBucket Bucket,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal BalanceQuantity,
    decimal CostPrice,
    decimal AverageCostAfterTransaction,
    string ReferenceType,
    Guid ReferenceId,
    string? Remarks);

public sealed record StockAdjustmentItemRequest(
    Guid ProductId,
    StockBucket Bucket,
    StockAdjustmentDirection Direction,
    [param: Range(typeof(decimal), "0.001", "999999999")] decimal Quantity,
    [param: Range(typeof(decimal), "0", "999999999")] decimal UnitCost);

public sealed record CreateStockAdjustmentRequest(
    [param: Required, MaxLength(500)] string Reason,
    [param: MaxLength(1000)] string? Notes,
    [param: Required, MinLength(1)] IReadOnlyCollection<StockAdjustmentItemRequest> Items);

public sealed record ReviewStockAdjustmentRequest(
    [param: MaxLength(1000)] string? Notes);

public sealed record RejectStockAdjustmentRequest(
    [param: Required, MaxLength(1000)] string Reason);

public sealed record ReverseStockAdjustmentRequest(
    [param: Required, MaxLength(1000)] string Reason);

public sealed record StockAdjustmentLineItem(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    StockBucket Bucket,
    StockAdjustmentDirection Direction,
    decimal Quantity,
    decimal UnitCost,
    decimal AppliedCost);

public sealed record StockAdjustmentDetailItem(
    Guid Id,
    string AdjustmentNumber,
    DateTimeOffset RequestedOn,
    string Reason,
    string? Notes,
    StockAdjustmentStatus Status,
    Guid RequestedBy,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedOn,
    string? ReviewNotes,
    Guid? ReversedBy,
    DateTimeOffset? ReversedOn,
    string? ReversalReason,
    IReadOnlyCollection<StockAdjustmentLineItem> Items);

public interface IInventoryService
{
    Task<OperationResult<OpeningStockResult>> RecordOpeningStockAsync(
        OpeningStockRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<PagedResult<CurrentStockItem>> GetCurrentStockAsync(
        string? search,
        bool lowStockOnly,
        bool damagedOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<PagedResult<StockLedgerItem>> GetLedgerAsync(
        Guid? productId,
        StockTransactionType? transactionType,
        StockBucket? bucket,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<OperationResult<StockAdjustmentDetailItem>> CreateAdjustmentAsync(
        CreateStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<PagedResult<StockAdjustmentDetailItem>> GetAdjustmentsAsync(
        StockAdjustmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<OperationResult<StockAdjustmentDetailItem>> GetAdjustmentAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<OperationResult<StockAdjustmentDetailItem>> ApproveAdjustmentAsync(
        Guid id,
        ReviewStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<StockAdjustmentDetailItem>> RejectAdjustmentAsync(
        Guid id,
        RejectStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<StockAdjustmentDetailItem>> ReverseAdjustmentAsync(
        Guid id,
        ReverseStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
}
