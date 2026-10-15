using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RetailShop.Application.Reports;
using RetailShop.Application.Security;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Domain.Returns;
using RetailShop.Domain.Sales;
using RetailShop.Domain.Warranty;
using RetailShop.Domain.Expenses;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Reports;

internal sealed class ReportService(
    RetailShopDbContext dbContext,
    IConfiguration configuration) : IReportService
{
    public async Task<DashboardSummary> GetDashboardAsync(
        CancellationToken cancellationToken)
    {
        var timeZone = GetBusinessTimeZone();
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        var todayStart = StartOfBusinessDay(localNow.Date, timeZone);
        var tomorrowStart = StartOfBusinessDay(localNow.Date.AddDays(1), timeZone);
        var monthStart = StartOfBusinessDay(
            new DateTime(localNow.Year, localNow.Month, 1), timeZone);

        var todaySales = await dbContext.Sales
            .AsNoTracking()
            .Where(item =>
                item.Status == SaleStatus.Completed &&
                item.SaleDate >= todayStart &&
                item.SaleDate < tomorrowStart)
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

    private TimeZoneInfo GetBusinessTimeZone()
    {
        var timeZoneId = configuration["BusinessTimeZone:Id"] ?? "Asia/Dhaka";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTimeOffset StartOfBusinessDay(DateTime date, TimeZoneInfo timeZone) =>
        new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified),
            timeZone.GetUtcOffset(date.Date)).ToUniversalTime();

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
                    (line.Quantity * line.CostPrice)),
                item.Details.Sum(line => line.DiscountAmount),
                item.Details.Sum(line => line.VatAmount)))
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

    public async Task<IReadOnlyCollection<SalesItemReportItem>> GetSalesItemsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (start, end) = ToRange(from, to);
        return await dbContext.SaleDetails.AsNoTracking()
            .Where(line => line.Sale.Status == SaleStatus.Completed && line.Sale.SaleDate >= start && line.Sale.SaleDate < end)
            .OrderBy(line => line.Sale.SaleDate).ThenBy(line => line.Sale.InvoiceNumber)
            .Select(line => new SalesItemReportItem(line.SaleId, line.Sale.InvoiceNumber, line.Sale.SaleDate,
                line.Sale.Customer == null ? string.Empty : line.Sale.Customer.Name,
                line.Product.Category == null ? null : line.Product.Category.Name, line.Product.ProductCode, line.Product.Name,
                line.Product.Unit.Symbol, line.Quantity, line.UnitPrice, line.DiscountAmount, line.VatAmount,
                (line.Quantity * line.UnitPrice) - line.DiscountAmount + line.VatAmount))
            .ToArrayAsync(cancellationToken);
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

    public IReadOnlyCollection<ManagementReportCatalogueItem> GetManagementReportCatalogue() =>
    [
        new(ManagementReportKind.DailySales, "Daily sales", "date", Permissions.Reports.Sales),
        new(ManagementReportKind.MonthlySales, "Monthly sales", "month", Permissions.Reports.Sales),
        new(ManagementReportKind.YearlySales, "Yearly sales", "year", Permissions.Reports.Sales),
        new(ManagementReportKind.Profit, "Profit", "range", Permissions.Reports.Profit),
        new(ManagementReportKind.Inventory, "Inventory", "search", Permissions.Reports.Inventory),
        new(ManagementReportKind.CustomerDues, "Customer dues", "none", Permissions.Reports.Sales)
        ,new(ManagementReportKind.ExpenseRegister, "Expense register", "range", Permissions.Expenses.Reports)
        ,new(ManagementReportKind.MonthlyExpenses, "Monthly expenses", "month", Permissions.Expenses.Reports)
        ,new(ManagementReportKind.ExpenseCategorySummary, "Expense category summary", "range", Permissions.Expenses.Reports)
    ];

    public async Task<ManagementReportPreview> GetManagementReportAsync(
        ManagementReportKind kind,
        ManagementReportRequest request,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetBusinessTimeZone()).Date);
        return kind switch
        {
            ManagementReportKind.DailySales => await SalesPreviewAsync(
                "Daily sales report", request.Date ?? today, request.Date ?? today, cancellationToken),
            ManagementReportKind.MonthlySales => await MonthlyPreviewAsync(request, today, cancellationToken),
            ManagementReportKind.YearlySales => await YearlyPreviewAsync(request, today, cancellationToken),
            ManagementReportKind.Profit => await ProfitPreviewAsync(request, today, cancellationToken),
            ManagementReportKind.Inventory => await InventoryPreviewAsync(request.Search, cancellationToken),
            ManagementReportKind.CustomerDues => await CustomerDuesPreviewAsync(cancellationToken),
            ManagementReportKind.ExpenseRegister => await ExpenseRegisterPreviewAsync(request, today, cancellationToken),
            ManagementReportKind.MonthlyExpenses => await ExpenseCategoryPreviewAsync(request, today, cancellationToken),
            ManagementReportKind.ExpenseCategorySummary => await ExpenseCategoryPreviewAsync(request, today, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Unsupported management report.")
        };
    }

    private async Task<ManagementReportPreview> MonthlyPreviewAsync(ManagementReportRequest request, DateOnly today, CancellationToken ct)
    {
        var year = request.Year is >= 2000 and <= 9999 ? request.Year.Value : today.Year;
        var month = request.Month is >= 1 and <= 12 ? request.Month.Value : today.Month;
        var from = new DateOnly(year, month, 1);
        return await SalesPreviewAsync("Monthly sales report", from, from.AddMonths(1).AddDays(-1), ct);
    }

    private async Task<ManagementReportPreview> SalesPreviewAsync(string title, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var sales = await GetSalesAsync(from, to, ct);
        var columns = new[]
        {
            new ManagementReportColumn("invoice", "Invoice"), new("date", "Date"), new("customer", "Customer"),
            new("total", "Total", true), new("discount", "Discount", true), new("tax", "Tax", true),
            new("paid", "Paid", true), new("due", "Due", true), new("status", "Status")
        };
        var details = await GetSalesItemsAsync(from, to, ct);
        var rows = sales.Select(item => Row(
            ("invoice", Text(item.InvoiceNumber)), ("date", Text(item.SaleDate.ToString("yyyy-MM-dd"))),
            ("customer", Text(item.CustomerName)), ("total", Money(item.GrandTotal)),
            ("discount", Money(item.DiscountAmount)), ("tax", Money(item.TaxAmount)), ("paid", Money(item.PaidAmount)),
            ("due", Money(item.DueAmount)), ("status", Text("Completed")), ("items", Text(string.Join("; ", details.Where(detail => detail.SaleId == item.Id).Select(detail => $"{detail.CategoryName ?? "Uncategorised"} · {detail.ProductCode} · {detail.ProductName} · {detail.Quantity:N2} {detail.UnitSymbol} × ৳{detail.UnitPrice:N2}, discount ৳{detail.LineDiscount:N2}, line ৳{detail.LineAmount:N2}")))))).ToArray();
        columns = columns.Append(new ManagementReportColumn("items", "Items")).ToArray();
        return new ManagementReportPreview(from == to ? ManagementReportKind.DailySales : ManagementReportKind.MonthlySales,
            title, Period(from, to), columns, rows,
            [Summary("Sales total", sales.Sum(x => x.GrandTotal)), Summary("Paid", sales.Sum(x => x.PaidAmount)), Summary("Due", sales.Sum(x => x.DueAmount))]);
    }

    private async Task<ManagementReportPreview> YearlyPreviewAsync(ManagementReportRequest request, DateOnly today, CancellationToken ct)
    {
        var year = request.Year is >= 2000 and <= 9999 ? request.Year.Value : today.Year;
        var sales = await GetSalesAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), ct);
        var rows = Enumerable.Range(1, 12).Select(month =>
        {
            var group = sales.Where(x => x.SaleDate.Month == month).ToArray();
            return Row(("month", Text(new DateTime(year, month, 1).ToString("MMMM"))), ("invoices", Number(group.Length)),
                ("total", Money(group.Sum(x => x.GrandTotal))), ("paid", Money(group.Sum(x => x.PaidAmount))), ("due", Money(group.Sum(x => x.DueAmount))));
        }).ToArray();
        return new ManagementReportPreview(ManagementReportKind.YearlySales, "Yearly sales report", year.ToString(),
            [new("month", "Month"), new("invoices", "Invoices", true), new("total", "Total", true), new("paid", "Paid", true), new("due", "Due", true)], rows,
            [Summary("Annual sales total", sales.Sum(x => x.GrandTotal)), Summary("Paid", sales.Sum(x => x.PaidAmount)), Summary("Due", sales.Sum(x => x.DueAmount))]);
    }

    private async Task<ManagementReportPreview> ProfitPreviewAsync(ManagementReportRequest request, DateOnly today, CancellationToken ct)
    {
        var from = request.From ?? today.AddDays(-30); var to = request.To ?? today;
        var report = await GetProfitAsync(from, to, ct);
        return new ManagementReportPreview(ManagementReportKind.Profit, "Profit report", Period(from, to),
            [new("revenue", "Revenue", true), new("cost", "Cost", true), new("profit", "Profit", true), new("margin", "Margin", true)],
            [Row(("revenue", Money(report.Revenue)), ("cost", Money(report.Cost)), ("profit", Money(report.Profit)), ("margin", Number(report.ProfitMarginPercent, "%")))],
            [Summary("Revenue", report.Revenue), Summary("Cost", report.Cost), Summary("Profit", report.Profit)]);
    }

    private async Task<ManagementReportPreview> InventoryPreviewAsync(string? search, CancellationToken ct)
    {
        var inventory = await GetInventoryAsync(search, ct);
        return new ManagementReportPreview(ManagementReportKind.Inventory, "Inventory report", string.IsNullOrWhiteSpace(search) ? "All products" : $"Search: {search.Trim()}",
            [new("code", "Code"), new("product", "Product"), new("available", "Available", true), new("averageCost", "Average cost", true), new("stockValue", "Stock value", true), new("minimum", "Minimum stock", true)],
            inventory.Select(x => Row(("code", Text(x.ProductCode)), ("product", Text(x.ProductName)), ("available", Number(x.AvailableQuantity, $" {x.UnitSymbol}")), ("averageCost", Money(x.AverageCost)), ("stockValue", Money(x.StockValue)), ("minimum", Number(x.MinimumStockLevel)))).ToArray(),
            [Summary("Stock value", inventory.Sum(x => x.StockValue)), new("Products", inventory.Count.ToString(), inventory.Count)]);
    }

    private async Task<ManagementReportPreview> CustomerDuesPreviewAsync(CancellationToken ct)
    {
        var dues = await GetCustomerDuesAsync(ct);
        return new ManagementReportPreview(ManagementReportKind.CustomerDues, "Customer due report", "Current outstanding balances",
            [new("code", "Customer code"), new("customer", "Customer"), new("phone", "Phone"), new("creditLimit", "Credit limit", true), new("balance", "Outstanding", true)],
            dues.Select(x => Row(("code", Text(x.CustomerCode)), ("customer", Text(x.CustomerName)), ("phone", Text(x.Phone)), ("creditLimit", Money(x.CreditLimit)), ("balance", Money(x.CurrentBalance)))).ToArray(),
            [Summary("Outstanding balance", dues.Sum(x => x.CurrentBalance)), new("Customers", dues.Count.ToString(), dues.Count)]);
    }

    private async Task<ManagementReportPreview> ExpenseRegisterPreviewAsync(ManagementReportRequest request, DateOnly today, CancellationToken ct)
    {
        var from = request.From ?? today.AddDays(-30); var to = request.To ?? today;
        var entries = await ExpenseLinesAsync(from, to, ct);
        return new ManagementReportPreview(ManagementReportKind.ExpenseRegister, "Expense register", Period(from, to),
            [new("date", "Date"), new("voucher", "Voucher"), new("payee", "Payee"), new("payment", "Payment method"), new("category", "Category"), new("description", "Description"), new("amount", "Amount", true)],
            entries.Select(x => Row(("date", Text(x.Date.ToString("yyyy-MM-dd"))), ("voucher", Text(x.VoucherNumber)), ("payee", Text(x.Payee)), ("payment", Text(x.PaymentMethod)), ("category", Text(x.Category)), ("description", Text(x.Description)), ("amount", Money(x.Amount)))).ToArray(),
            [Summary("Posted operating expenses", entries.Sum(x => x.Amount)), new("Lines", entries.Count.ToString(), entries.Count)]);
    }

    private async Task<ManagementReportPreview> ExpenseCategoryPreviewAsync(ManagementReportRequest request, DateOnly today, CancellationToken ct)
    {
        var from = request.From ?? (request.Year is int year && request.Month is int month ? new DateOnly(year, month, 1) : today.AddDays(-30));
        var to = request.To ?? (request.Year is int monthlyYear && request.Month is int monthlyMonth ? new DateOnly(monthlyYear, monthlyMonth, 1).AddMonths(1).AddDays(-1) : today);
        var entries = await ExpenseLinesAsync(from, to, ct);
        var groups = entries.GroupBy(x => x.Category).OrderBy(x => x.Key).ToArray();
        var kind = request.Month is not null && request.Year is not null ? ManagementReportKind.MonthlyExpenses : ManagementReportKind.ExpenseCategorySummary;
        return new ManagementReportPreview(kind, kind == ManagementReportKind.MonthlyExpenses ? "Monthly expense report" : "Expense category summary", Period(from, to),
            [new("category", "Category"), new("amount", "Amount", true)], groups.Select(x => Row(("category", Text(x.Key)), ("amount", Money(x.Sum(item => item.Amount))))).ToArray(),
            [Summary("Posted operating expenses", entries.Sum(x => x.Amount)), new("Categories", groups.Length.ToString(), groups.Length)]);
    }

    private async Task<IReadOnlyCollection<ExpenseLine>> ExpenseLinesAsync(DateOnly from, DateOnly to, CancellationToken ct) => await dbContext.ExpenseVoucherDetails.AsNoTracking()
        .Where(detail => detail.Voucher.Status == ExpenseVoucherStatus.Posted && detail.Voucher.ExpenseDate >= from && detail.Voucher.ExpenseDate <= to)
        .OrderBy(detail => detail.Voucher.ExpenseDate).ThenBy(detail => detail.Voucher.VoucherNumber)
        .Select(detail => new ExpenseLine(detail.Voucher.ExpenseDate, detail.Voucher.VoucherNumber, detail.Voucher.Payee, detail.Voucher.PaymentMethod == null ? string.Empty : detail.Voucher.PaymentMethod.Name, detail.Category.Name, detail.Description, detail.Amount)).ToArrayAsync(ct);

    private sealed record ExpenseLine(DateOnly Date, string VoucherNumber, string Payee, string PaymentMethod, string Category, string Description, decimal Amount);

    private static ManagementReportRow Row(params (string Key, ManagementReportCell Value)[] values) => new(values.ToDictionary(x => x.Key, x => x.Value));
    private static ManagementReportCell Text(string value) => new(value ?? string.Empty);
    private static ManagementReportCell Money(decimal value) => new($"৳{value:N2}", value);
    private static ManagementReportCell Number(decimal value, string suffix = "") => new($"{value:N2}{suffix}", value);
    private static ManagementReportCell Number(int value) => new(value.ToString(), value);
    private static ManagementReportSummary Summary(string label, decimal value) => new(label, Money(value).Display, value);
    private static string Period(DateOnly from, DateOnly to) => from == to ? from.ToString("dd MMMM yyyy") : $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";

    private (DateTimeOffset Start, DateTimeOffset End) ToRange(
        DateOnly from,
        DateOnly to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var timeZone = GetBusinessTimeZone();
        var start = StartOfBusinessDay(from.ToDateTime(TimeOnly.MinValue), timeZone);
        var end = StartOfBusinessDay(to.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);
        return (start, end);
    }
}
