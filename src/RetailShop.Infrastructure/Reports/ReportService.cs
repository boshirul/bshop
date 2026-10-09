using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Reports;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Domain.Returns;
using RetailShop.Domain.Sales;
using RetailShop.Domain.Warranty;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Reports;

internal sealed class ReportService(RetailShopDbContext dbContext) : IReportService
{
    public async Task<DashboardSummary> GetDashboardAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        var monthStart = new DateTimeOffset(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);

        var todaySales = await dbContext.Sales
            .AsNoTracking()
            .Where(item =>
                item.Status == SaleStatus.Completed &&
                item.SaleDate >= todayStart)
            .SumAsync(item => (decimal?)item.GrandTotal, cancellationToken) ?? 0;
        var monthSales = await dbContext.Sales
            .AsNoTracking()
            .Where(item =>
                item.Status == SaleStatus.Completed &&
                item.SaleDate >= monthStart)
            .SumAsync(item => (decimal?)item.GrandTotal, cancellationToken) ?? 0;
        var customerDue = await dbContext.CustomerLedgerEntries
            .AsNoTracking()
            .SumAsync(
                item => (decimal?)(item.Debit - item.Credit),
                cancellationToken) ?? 0;
        var stockValue = await dbContext.StockBalances
            .AsNoTracking()
            .SumAsync(
                item => (decimal?)(
                    (item.AvailableQuantity +
                     item.ReservedQuantity +
                     item.DamagedQuantity +
                     item.WarrantyQuantity +
                     item.SupplierClaimQuantity) *
                    item.Product.AverageCost),
                cancellationToken) ?? 0;
        var lowStock = await dbContext.StockBalances
            .AsNoTracking()
            .CountAsync(
                item => item.AvailableQuantity <= item.Product.MinimumStockLevel,
                cancellationToken);
        var pendingOrders = await dbContext.OnlineOrders
            .AsNoTracking()
            .CountAsync(
                item => item.Status == OnlineOrderStatus.Pending,
                cancellationToken);
        var pendingReturns = await dbContext.SalesReturns
            .AsNoTracking()
            .CountAsync(
                item => item.Status == SalesReturnStatus.Pending,
                cancellationToken);
        var pendingWarranty = await dbContext.WarrantyClaims
            .AsNoTracking()
            .CountAsync(
                item => item.Status == WarrantyClaimStatus.Pending,
                cancellationToken);

        return new DashboardSummary(
            todaySales,
            monthSales,
            customerDue,
            stockValue,
            lowStock,
            pendingOrders,
            pendingReturns,
            pendingWarranty);
    }

    public async Task<IReadOnlyCollection<SalesReportItem>> GetSalesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ToRange(from, to);
        return await dbContext.Sales
            .AsNoTracking()
            .Where(item =>
                item.Status == SaleStatus.Completed &&
                item.SaleDate >= start &&
                item.SaleDate < end)
            .OrderByDescending(item => item.SaleDate)
            .Select(item => new SalesReportItem(
                item.Id,
                item.InvoiceNumber,
                item.SaleDate,
                item.Customer == null ? "Walk-in customer" : item.Customer.Name,
                item.GrandTotal,
                item.PaidAmount,
                Math.Max(
                    0,
                    item.GrandTotal - item.ReturnedAmount -
                    (item.PaidAmount - item.RefundedAmount)),
                item.Details.Sum(line =>
                    ((line.Quantity * line.UnitPrice) -
                     line.DiscountAmount +
                     line.VatAmount) -
                    (line.Quantity * line.CostPrice))))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProfitSummary> GetProfitAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var (start, end) = ToRange(from, to);
        var rows = await dbContext.SaleDetails
            .AsNoTracking()
            .Where(item =>
                item.Sale.Status == SaleStatus.Completed &&
                item.Sale.SaleDate >= start &&
                item.Sale.SaleDate < end)
            .Select(item => new
            {
                Revenue = (item.Quantity * item.UnitPrice) -
                    item.DiscountAmount +
                    item.VatAmount,
                Cost = item.Quantity * item.CostPrice
            })
            .ToArrayAsync(cancellationToken);

        var revenue = rows.Sum(item => item.Revenue);
        var cost = rows.Sum(item => item.Cost);
        var profit = revenue - cost;
        var margin = revenue == 0
            ? 0
            : decimal.Round((profit / revenue) * 100, 2, MidpointRounding.AwayFromZero);
        return new ProfitSummary(revenue, cost, profit, margin);
    }

    public async Task<IReadOnlyCollection<InventoryReportItem>> GetInventoryAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.StockBalances.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.ILike(item.Product.Name, $"%{term}%") ||
                EF.Functions.ILike(item.Product.ProductCode, $"%{term}%"));
        }

        return await query
            .OrderBy(item => item.Product.Name)
            .Select(item => new InventoryReportItem(
                item.ProductId,
                item.Product.ProductCode,
                item.Product.Name,
                item.Product.Unit.Symbol,
                item.AvailableQuantity,
                item.ReservedQuantity,
                item.DamagedQuantity,
                item.WarrantyQuantity,
                item.SupplierClaimQuantity,
                item.TotalQuantity,
                item.Product.AverageCost,
                item.TotalQuantity * item.Product.AverageCost,
                item.Product.MinimumStockLevel))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CustomerDueReportItem>> GetCustomerDuesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Customers
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.CustomerCode,
                item.Name,
                item.Phone,
                item.CreditLimit,
                CurrentBalance = dbContext.CustomerLedgerEntries
                    .Where(entry => entry.CustomerId == item.Id)
                    .Sum(entry => (decimal?)(entry.Debit - entry.Credit)) ?? 0
            })
            .Where(item => item.CurrentBalance > 0)
            .OrderByDescending(item => item.CurrentBalance)
            .Select(item => new CustomerDueReportItem(
                item.Id,
                item.CustomerCode,
                item.Name,
                item.Phone,
                item.CreditLimit,
                item.CurrentBalance))
            .ToArrayAsync(cancellationToken);
        return rows;
    }

    public async Task<OperationalReport> GetOperationsAsync(
        CancellationToken cancellationToken)
    {
        var onlineOrders = await dbContext.OnlineOrders
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new StatusCountItem<OnlineOrderStatus>(
                group.Key,
                group.Count()))
            .ToArrayAsync(cancellationToken);
        var returns = await dbContext.SalesReturns
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new StatusCountItem<SalesReturnStatus>(
                group.Key,
                group.Count()))
            .ToArrayAsync(cancellationToken);
        var warranty = await dbContext.WarrantyClaims
            .AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new StatusCountItem<WarrantyClaimStatus>(
                group.Key,
                group.Count()))
            .ToArrayAsync(cancellationToken);
        return new OperationalReport(onlineOrders, returns, warranty);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) ToRange(
        DateOnly from,
        DateOnly to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var start = new DateTimeOffset(
            from.Year,
            from.Month,
            from.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        var inclusiveEnd = new DateTimeOffset(
            to.Year,
            to.Month,
            to.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        return (start, inclusiveEnd.AddDays(1));
    }
}
