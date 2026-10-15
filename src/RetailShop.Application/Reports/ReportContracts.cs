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
    decimal Profit,
    decimal DiscountAmount = 0,
    decimal TaxAmount = 0);

public sealed record SalesItemReportItem(
    Guid SaleId, string InvoiceNumber, DateTimeOffset SaleDate, string CustomerName,
    string? CategoryName, string ProductCode, string ProductName, string UnitSymbol,
    decimal Quantity, decimal UnitPrice, decimal LineDiscount, decimal TaxAmount, decimal LineAmount);

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

public enum ManagementReportKind
{
    DailySales,
    MonthlySales,
    YearlySales,
    Profit,
    Inventory,
    CustomerDues,
    ExpenseRegister,
    MonthlyExpenses,
    ExpenseCategorySummary
}

public sealed record ManagementReportRequest(
    DateOnly? Date,
    int? Month,
    int? Year,
    DateOnly? From,
    DateOnly? To,
    string? Search);

public sealed record ManagementReportColumn(string Key, string Heading, bool IsNumeric = false);

public sealed record ManagementReportCell(string Display, decimal? Numeric = null);

public sealed record ManagementReportRow(IReadOnlyDictionary<string, ManagementReportCell> Values);

public sealed record ManagementReportSummary(string Label, string Value, decimal? Numeric = null);

public sealed record ManagementReportPreview(
    ManagementReportKind Kind,
    string Title,
    string PeriodLabel,
    IReadOnlyCollection<ManagementReportColumn> Columns,
    IReadOnlyCollection<ManagementReportRow> Rows,
    IReadOnlyCollection<ManagementReportSummary> Summary);

public sealed record ManagementReportCatalogueItem(
    ManagementReportKind Kind,
    string DisplayName,
    string FilterType,
    string Permission);

public interface IReportService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SalesReportItem>> GetSalesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SalesItemReportItem>> GetSalesItemsAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken);

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

    Task<ManagementReportPreview> GetManagementReportAsync(
        ManagementReportKind kind,
        ManagementReportRequest request,
        CancellationToken cancellationToken);

    IReadOnlyCollection<ManagementReportCatalogueItem> GetManagementReportCatalogue();
}
