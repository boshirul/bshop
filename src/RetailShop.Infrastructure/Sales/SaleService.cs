using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Sales;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Products;
using RetailShop.Domain.Sales;
using RetailShop.Domain.Warranty;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Sales;

internal sealed class SaleService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : ISaleService
{
    public async Task<PagedResult<SaleListItem>> GetSalesAsync(
        string? search,
        Guid? customerId,
        SaleStatus? status,
        SalePaymentStatus? paymentStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Sales.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(sale =>
                EF.Functions.ILike(sale.InvoiceNumber, $"%{term}%") ||
                (sale.Customer != null &&
                 (EF.Functions.ILike(sale.Customer.Name, $"%{term}%") ||
                  EF.Functions.ILike(sale.Customer.Phone, $"%{term}%"))));
        }
        if (customerId.HasValue)
        {
            query = query.Where(sale => sale.CustomerId == customerId);
        }
        if (status.HasValue)
        {
            query = query.Where(sale => sale.Status == status);
        }
        if (paymentStatus.HasValue)
        {
            query = query.Where(sale => sale.PaymentStatus == paymentStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(sale => sale.SaleDate)
            .ThenByDescending(sale => sale.InvoiceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sale => new SaleListItem(
                sale.Id,
                sale.InvoiceNumber,
                sale.CustomerId,
                sale.Customer == null ? "Walk-in customer" : sale.Customer.Name,
                sale.SaleDate,
                sale.Status,
                sale.PaymentStatus,
                sale.GrandTotal,
                sale.PaidAmount,
                sale.Status == SaleStatus.Cancelled
                    ? 0
                    : sale.GrandTotal - sale.ReturnedAmount -
                      (sale.PaidAmount - sale.RefundedAmount)))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<SaleListItem>(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<OperationResult<SaleDetailItem>> GetSaleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectSale(
                dbContext.Sales.AsNoTracking().Where(sale => sale.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<SaleDetailItem>.Failure("Sale not found.")
            : OperationResult<SaleDetailItem>.Success(item);
    }

    public async Task<OperationResult<SaleDetailItem>> CreateSaleAsync(
        CreateSaleRequest request,
        Guid performedBy,
        bool canChangePrice,
        bool canDiscount,
        bool canSellOnDue,
        CancellationToken cancellationToken)
    {
        var basicError = ValidateBasicRequest(request, canDiscount);
        if (basicError is not null)
        {
            return OperationResult<SaleDetailItem>.Failure(basicError);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var customer = request.CustomerId.HasValue
            ? await dbContext.Customers.SingleOrDefaultAsync(
                item => item.Id == request.CustomerId.Value && item.IsActive,
                cancellationToken)
            : null;
        if (request.CustomerId.HasValue && customer is null)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Customer is missing or inactive.");
        }

        var invoiceSetting = await dbContext.InvoiceSettings.SingleOrDefaultAsync(
            cancellationToken);
        var invoiceNumber = invoiceSetting is null
            ? GenerateFallbackInvoiceNumber()
            : $"{invoiceSetting.InvoicePrefix}-{invoiceSetting.NextInvoiceNumber:D6}";
        if (invoiceSetting is not null)
        {
            invoiceSetting.NextInvoiceNumber++;
            invoiceSetting.LastModifiedBy = performedBy;
        }

        var sale = new Sale(
            invoiceNumber,
            request.CustomerId,
            request.SaleDate,
            request.Notes)
        {
            CreatedBy = performedBy
        };

        foreach (var item in request.Items)
        {
            var product = await dbContext.Products.SingleOrDefaultAsync(
                product => product.Id == item.ProductId && product.IsActive,
                cancellationToken);
            if (product is null)
            {
                return OperationResult<SaleDetailItem>.Failure(
                    "One or more products are missing or inactive.");
            }
            if (!canChangePrice &&
                decimal.Round(item.UnitPrice, 2) !=
                decimal.Round(product.SalePrice, 2))
            {
                return OperationResult<SaleDetailItem>.Failure(
                    $"You do not have permission to change the price of '{product.Name}'.");
            }

            sale.AddDetail(
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount,
                item.VatAmount,
                product.AverageCost);
        }

        var paymentTotal = request.Payments.Sum(payment => payment.Amount);
        if (paymentTotal > sale.GrandTotal)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Payments cannot exceed the sale total.");
        }
        var dueAmount = sale.GrandTotal - paymentTotal;
        if (dueAmount > 0)
        {
            if (!canSellOnDue)
            {
                return OperationResult<SaleDetailItem>.Failure(
                    "You do not have permission to complete a sale with an outstanding balance.");
            }
            if (customer is null)
            {
                return OperationResult<SaleDetailItem>.Failure(
                    "Select a customer before completing a sale on due.");
            }

            var existingBalance = await dbContext.CustomerLedgerEntries
                .Where(entry => entry.CustomerId == customer.Id)
                .SumAsync(
                    entry => (decimal?)(entry.Debit - entry.Credit),
                    cancellationToken) ?? 0;
            if (existingBalance + dueAmount > customer.CreditLimit)
            {
                return OperationResult<SaleDetailItem>.Failure(
                    $"This sale exceeds the customer's credit limit of {customer.CreditLimit:0.00}.");
            }
        }

        var paymentMethodIds = request.Payments
            .Select(payment => payment.PaymentMethodId)
            .Distinct()
            .ToArray();
        if (paymentMethodIds.Length > 0 &&
            await dbContext.PaymentMethods.CountAsync(
                method => paymentMethodIds.Contains(method.Id) && method.IsActive,
                cancellationToken) != paymentMethodIds.Length)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "One or more payment methods are missing or inactive.");
        }

        dbContext.Sales.Add(sale);
        foreach (var detail in sale.Details)
        {
            var product = await dbContext.Products.SingleAsync(
                item => item.Id == detail.ProductId,
                cancellationToken);
            var stockError = await IssueStockAsync(
                product,
                detail.Quantity,
                detail.CostPrice,
                sale.Id,
                sale.InvoiceNumber,
                performedBy,
                cancellationToken);
            if (stockError is not null)
            {
                return OperationResult<SaleDetailItem>.Failure(stockError);
            }
            var serialError = await ApplySerialsAsync(
                sale,
                detail,
                product,
                request.Items.Single(item => item.ProductId == detail.ProductId),
                performedBy,
                cancellationToken);
            if (serialError is not null)
            {
                return OperationResult<SaleDetailItem>.Failure(serialError);
            }
        }

        if (sale.CustomerId.HasValue)
        {
            AddCustomerLedger(
                sale.CustomerId.Value,
                sale.SaleDate,
                CustomerLedgerEntryType.Sale,
                sale.GrandTotal,
                0,
                "Sale",
                sale.Id,
                sale.InvoiceNumber,
                performedBy,
                sale.Notes);
        }

        foreach (var item in request.Payments)
        {
            var payment = new SalePayment(
                sale.Id,
                item.PaymentMethodId,
                item.Amount,
                sale.SaleDate,
                performedBy,
                item.ReferenceNumber,
                item.Notes);
            sale.AddPayment(payment);
            if (sale.CustomerId.HasValue)
            {
                AddPaymentLedger(sale, payment, performedBy);
            }
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<SaleDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Create",
            "Sale",
            sale.Id.ToString(),
            $"Completed sale {sale.InvoiceNumber}.",
            performedBy,
            cancellationToken);
        return await GetSaleAsync(sale.Id, cancellationToken);
    }

    public async Task<OperationResult<SaleDetailItem>> RecordPaymentAsync(
        Guid saleId,
        RecordSalePaymentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Payment amount must be greater than zero.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var sale = await dbContext.Sales.SingleOrDefaultAsync(
            item => item.Id == saleId,
            cancellationToken);
        if (sale is null)
        {
            return OperationResult<SaleDetailItem>.Failure("Sale not found.");
        }
        if (sale.Status == SaleStatus.Cancelled)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "A cancelled sale cannot receive payments.");
        }
        if (request.Amount > sale.DueAmount)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Payment cannot exceed the outstanding sale amount.");
        }
        if (!await dbContext.PaymentMethods.AnyAsync(
                method => method.Id == request.PaymentMethodId && method.IsActive,
                cancellationToken))
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Payment method is missing or inactive.");
        }

        var payment = new SalePayment(
            sale.Id,
            request.PaymentMethodId,
            request.Amount,
            request.PaidOn,
            performedBy,
            request.ReferenceNumber,
            request.Notes);
        sale.AddPayment(payment);
        dbContext.SalePayments.Add(payment);
        if (sale.CustomerId.HasValue)
        {
            AddPaymentLedger(sale, payment, performedBy);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<SaleDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Payment",
            "Sale",
            sale.Id.ToString(),
            $"Collected payment for {sale.InvoiceNumber}.",
            performedBy,
            cancellationToken);
        return await GetSaleAsync(sale.Id, cancellationToken);
    }

    public async Task<OperationResult<SaleDetailItem>> CancelSaleAsync(
        Guid saleId,
        CancelSaleRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var sale = await dbContext.Sales
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == saleId, cancellationToken);
        if (sale is null)
        {
            return OperationResult<SaleDetailItem>.Failure("Sale not found.");
        }

        try
        {
            sale.Cancel(performedBy, request.Reason);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<SaleDetailItem>.Failure(exception.Message);
        }

        foreach (var detail in sale.Details)
        {
            var product = await dbContext.Products.SingleAsync(
                item => item.Id == detail.ProductId,
                cancellationToken);
            await RestoreStockAsync(
                product,
                detail.Quantity,
                detail.CostPrice,
                sale.Id,
                sale.InvoiceNumber,
                performedBy,
                request.Reason,
                cancellationToken);
        }

        if (sale.CustomerId.HasValue)
        {
            AddCustomerLedger(
                sale.CustomerId.Value,
                sale.CancelledOn!.Value,
                CustomerLedgerEntryType.Cancellation,
                0,
                sale.GrandTotal,
                "SaleCancellation",
                sale.Id,
                sale.InvoiceNumber,
                performedBy,
                request.Reason);
            if (sale.PaidAmount > 0)
            {
                AddCustomerLedger(
                    sale.CustomerId.Value,
                    sale.CancelledOn.Value,
                    CustomerLedgerEntryType.Refund,
                    sale.PaidAmount,
                    0,
                    "SaleRefund",
                    sale.Id,
                    sale.InvoiceNumber,
                    performedBy,
                    request.Reason);
            }
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<SaleDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Cancel",
            "Sale",
            sale.Id.ToString(),
            $"Cancelled sale {sale.InvoiceNumber}: {request.Reason.Trim()}",
            performedBy,
            cancellationToken);
        return await GetSaleAsync(sale.Id, cancellationToken);
    }

    public async Task<OperationResult<CustomerLedgerResult>> GetCustomerLedgerAsync(
        Guid customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(item => item.Id == customerId)
            .Select(item => new
            {
                item.Id,
                item.CustomerCode,
                item.Name,
                item.CreditLimit
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerLedgerResult>.Failure("Customer not found.");
        }

        var entries = await dbContext.CustomerLedgerEntries
            .AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderBy(item => item.EntryDate)
            .ThenBy(item => item.CreatedOn)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var balance = 0m;
        var calculated = entries.Select(item =>
        {
            balance += item.Debit - item.Credit;
            return new CustomerLedgerItem(
                item.Id,
                item.EntryDate,
                item.EntryType,
                item.Debit,
                item.Credit,
                balance,
                item.ReferenceType,
                item.ReferenceId,
                item.ReferenceNumber,
                item.Notes);
        }).ToArray();
        var currentBalance = balance;
        var filtered = calculated
            .Where(item => !from.HasValue || item.EntryDate >= from)
            .Where(item => !to.HasValue || item.EntryDate <= to)
            .OrderByDescending(item => item.EntryDate)
            .ThenByDescending(item => item.Id)
            .ToArray();
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return OperationResult<CustomerLedgerResult>.Success(
            new CustomerLedgerResult(
                customer.Id,
                customer.CustomerCode,
                customer.Name,
                customer.CreditLimit,
                currentBalance,
                new PagedResult<CustomerLedgerItem>(
                    items,
                    page,
                    pageSize,
                    filtered.Length)));
    }

    private static string? ValidateBasicRequest(
        CreateSaleRequest request,
        bool canDiscount)
    {
        if (request.Items.Count == 0)
        {
            return "Add at least one item to the sale.";
        }
        if (request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        {
            return "Each product can appear only once in a sale.";
        }
        if (request.Items.Any(item =>
                item.Quantity <= 0 ||
                item.UnitPrice < 0 ||
                item.DiscountAmount < 0 ||
                item.VatAmount < 0 ||
                item.DiscountAmount > item.Quantity * item.UnitPrice))
        {
            return "Sale line quantities and amounts are invalid.";
        }
        if (!canDiscount && request.Items.Any(item => item.DiscountAmount > 0))
        {
            return "You do not have permission to apply sale discounts.";
        }
        if (request.Payments.Any(payment => payment.Amount <= 0))
        {
            return "Payment amounts must be greater than zero.";
        }
        return null;
    }

    private async Task<string?> IssueStockAsync(
        Product product,
        decimal quantity,
        decimal cost,
        Guid referenceId,
        string referenceNumber,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.ProductId == product.Id,
            cancellationToken);
        if (balance is null || balance.AvailableQuantity < quantity)
        {
            return $"Insufficient available stock for '{product.Name}'.";
        }

        balance.Decrease(StockBucket.Available, quantity);
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            StockTransactionType.Sale,
            StockBucket.Available,
            0,
            quantity,
            balance.AvailableQuantity,
            cost,
            product.AverageCost,
            "Sale",
            referenceId,
            performedBy,
            referenceNumber));
        return null;
    }

    private async Task<string?> ApplySerialsAsync(
        Sale sale,
        SaleDetail detail,
        Product product,
        CreateSaleItemRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var serialNumbers = request.SerialNumbers?
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (!product.IsSerialRequired)
        {
            return serialNumbers.Length > 0
                ? $"'{product.Name}' does not require serial numbers."
                : null;
        }
        if (detail.Quantity != decimal.Truncate(detail.Quantity))
        {
            return $"'{product.Name}' requires whole-unit quantities because serial numbers are tracked.";
        }
        if (serialNumbers.Length != (int)detail.Quantity)
        {
            return $"Select {detail.Quantity:0} serial number(s) for '{product.Name}'.";
        }
        if (!product.IsWarrantyAvailable || !product.WarrantyMonths.HasValue)
        {
            return $"'{product.Name}' requires a warranty period before serialized sale.";
        }

        var normalized = serialNumbers.Select(ProductSerial.Normalize).ToArray();
        var serials = await dbContext.ProductSerials
            .Where(item =>
                item.ProductId == product.Id &&
                normalized.Contains(item.NormalizedSerialNumber))
            .ToArrayAsync(cancellationToken);
        if (serials.Length != normalized.Length)
        {
            return $"One or more serial numbers for '{product.Name}' were not found.";
        }
        var unavailable = serials.FirstOrDefault(
            item => item.Status != ProductSerialStatus.Available);
        if (unavailable is not null)
        {
            return $"Serial '{unavailable.SerialNumber}' is not available.";
        }

        foreach (var serial in serials)
        {
            serial.MarkSold(
                sale.Id,
                detail.Id,
                sale.CustomerId,
                sale.SaleDate,
                product.WarrantyMonths,
                performedBy);
        }

        return null;
    }

    private async Task RestoreStockAsync(
        Product product,
        decimal quantity,
        decimal cost,
        Guid referenceId,
        string referenceNumber,
        Guid performedBy,
        string reason,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.ProductId == product.Id,
            cancellationToken);
        if (balance is null)
        {
            balance = new StockBalance(product.Id);
            dbContext.StockBalances.Add(balance);
        }

        var costedQuantity = balance.AvailableQuantity + balance.ReservedQuantity;
        product.AverageCost = CalculateWeightedAverage(
            costedQuantity,
            product.AverageCost,
            quantity,
            cost);
        product.LastModifiedBy = performedBy;
        balance.Increase(StockBucket.Available, quantity);
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            StockTransactionType.SaleCancellation,
            StockBucket.Available,
            quantity,
            0,
            balance.AvailableQuantity,
            cost,
            product.AverageCost,
            "SaleCancellation",
            referenceId,
            performedBy,
            $"{referenceNumber}: {reason}"));
    }

    private void AddPaymentLedger(
        Sale sale,
        SalePayment payment,
        Guid performedBy) =>
        AddCustomerLedger(
            sale.CustomerId!.Value,
            payment.PaidOn,
            CustomerLedgerEntryType.Payment,
            0,
            payment.Amount,
            "SalePayment",
            payment.Id,
            sale.InvoiceNumber,
            performedBy,
            payment.ReferenceNumber);

    private void AddCustomerLedger(
        Guid customerId,
        DateTimeOffset entryDate,
        CustomerLedgerEntryType entryType,
        decimal debit,
        decimal credit,
        string referenceType,
        Guid referenceId,
        string referenceNumber,
        Guid performedBy,
        string? notes) =>
        dbContext.CustomerLedgerEntries.Add(new CustomerLedgerEntry(
            customerId,
            entryDate,
            entryType,
            debit,
            credit,
            referenceType,
            referenceId,
            referenceNumber,
            performedBy,
            notes));

    private async Task<string?> SaveTransactionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return "Stock or customer data changed concurrently. Reload and try again.";
        }
        catch (DbUpdateException)
        {
            return "The sale could not be saved because a conflicting record exists.";
        }
    }

    private static decimal CalculateWeightedAverage(
        decimal existingQuantity,
        decimal existingCost,
        decimal incomingQuantity,
        decimal incomingCost)
    {
        var totalQuantity = existingQuantity + incomingQuantity;
        return totalQuantity == 0
            ? 0
            : decimal.Round(
                ((existingQuantity * existingCost) +
                 (incomingQuantity * incomingCost)) / totalQuantity,
                4,
                MidpointRounding.AwayFromZero);
    }

    private static string GenerateFallbackInvoiceNumber() =>
        $"INV-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<SaleDetailItem> ProjectSale(IQueryable<Sale> query) =>
        query.Select(sale => new SaleDetailItem(
            sale.Id,
            sale.InvoiceNumber,
            sale.CustomerId,
            sale.Customer == null ? "Walk-in customer" : sale.Customer.Name,
            sale.Customer == null ? null : sale.Customer.Phone,
            sale.SaleDate,
            sale.Status,
            sale.PaymentStatus,
            sale.Subtotal,
            sale.DiscountAmount,
            sale.VatAmount,
            sale.GrandTotal,
            sale.PaidAmount,
            sale.Status == SaleStatus.Cancelled
                ? 0
                : sale.GrandTotal - sale.ReturnedAmount -
                  (sale.PaidAmount - sale.RefundedAmount),
            sale.Details.Sum(item =>
                ((item.Quantity * item.UnitPrice) -
                 item.DiscountAmount +
                 item.VatAmount) -
                (item.Quantity * item.CostPrice)),
            sale.Notes,
            sale.CancellationReason,
            sale.CancelledOn,
            sale.Details
                .OrderBy(item => item.Product.Name)
                .Select(item => new SaleLineItem(
                    item.Id,
                    item.ProductId,
                    item.Product.ProductCode,
                    item.Product.Name,
                    item.Product.Unit.Symbol,
                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.VatAmount,
                    (item.Quantity * item.UnitPrice) -
                        item.DiscountAmount +
                        item.VatAmount,
                    item.CostPrice,
                    ((item.Quantity * item.UnitPrice) -
                     item.DiscountAmount +
                     item.VatAmount) -
                        (item.Quantity * item.CostPrice)))
                .ToArray(),
            sale.Payments
                .OrderByDescending(item => item.PaidOn)
                .Select(item => new SalePaymentItem(
                    item.Id,
                    item.PaymentMethodId,
                    item.PaymentMethod.Name,
                    item.Amount,
                    item.PaidOn,
                    item.ReferenceNumber,
                    item.Notes))
                .ToArray()));
}
