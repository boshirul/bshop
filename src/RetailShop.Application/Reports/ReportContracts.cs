using RetailShop.Domain.OnlineOrders;
using RetailShop.Domain.Returns;
using RetailShop.Domain.Warranty;

namespace RetailShop.Application.Reports;

public sealed record DashboardSummary(
    decimal TodaySales,
    decimal MonthSales,
    decimal CustomerDue,
    decimal StockValue,
    int LowStockProducts,
    int PendingOnlineOrders,
    int PendingReturns,
    int PendingWarrantyClaims);

public sealed record SalesReportItem(
    Guid Id,
    string InvoiceNumber,
    DateTimeOffset SaleDate,
    string CustomerName,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal DueAmount,
    decimal Profit);

public sealed record ProfitSummary(
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal ProfitMarginPercent);

public sealed record InventoryReportItem(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitSymbol,
    decimal AvailableQuantity,
    decimal ReservedQuantity,
    decimal DamagedQuantity,
    decimal WarrantyQuantity,
    decimal SupplierClaimQuantity,
    decimal TotalQuantity,
    decimal AverageCost,
    decimal StockValue,
    decimal MinimumStockLevel);

public sealed record CustomerDueReportItem(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string Phone,
    decimal CreditLimit,
    decimal CurrentBalance);

public sealed record StatusCountItem<TStatus>(
    TStatus Status,
    int Count);

public sealed record OperationalReport(
    IReadOnlyCollection<StatusCountItem<OnlineOrderStatus>> OnlineOrders,
    IReadOnlyCollection<StatusCountItem<SalesReturnStatus>> Returns,
    IReadOnlyCollection<StatusCountItem<WarrantyClaimStatus>> WarrantyClaims);

public interface IReportService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SalesReportItem>> GetSalesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<ProfitSummary> GetProfitAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<InventoryReportItem>> GetInventoryAsync(
        string? search,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CustomerDueReportItem>> GetCustomerDuesAsync(
        CancellationToken cancellationToken);

    Task<OperationalReport> GetOperationsAsync(CancellationToken cancellationToken);
}
