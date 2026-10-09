using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Purchases;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Products;
using RetailShop.Domain.Purchases;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Purchases;

internal sealed class PurchaseService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IPurchaseService
{
    public async Task<PagedResult<PurchaseListItem>> GetPurchasesAsync(
        string? search,
        Guid? supplierId,
        PurchaseStatus? status,
        PurchasePaymentStatus? paymentStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Purchases.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(purchase =>
                EF.Functions.ILike(purchase.PurchaseNumber, $"%{term}%") ||
                EF.Functions.ILike(purchase.Supplier.Name, $"%{term}%") ||
                (purchase.SupplierInvoiceNumber != null &&
                 EF.Functions.ILike(purchase.SupplierInvoiceNumber, $"%{term}%")));
        }
        if (supplierId.HasValue)
        {
            query = query.Where(purchase => purchase.SupplierId == supplierId);
        }
        if (status.HasValue)
        {
            query = query.Where(purchase => purchase.Status == status);
        }
        if (paymentStatus.HasValue)
        {
            query = query.Where(purchase => purchase.PaymentStatus == paymentStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(purchase => purchase.PurchaseDate)
            .ThenByDescending(purchase => purchase.PurchaseNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(purchase => new PurchaseListItem(
                purchase.Id,
                purchase.PurchaseNumber,
                purchase.SupplierId,
                purchase.Supplier.Name,
                purchase.SupplierInvoiceNumber,
                purchase.PurchaseDate,
                purchase.Status,
                purchase.PaymentStatus,
                purchase.GrandTotal,
                purchase.ReturnedAmount,
                purchase.PaidAmount,
                purchase.GrandTotal - purchase.ReturnedAmount - purchase.PaidAmount))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<PurchaseListItem>(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<OperationResult<PurchaseDetailItem>> GetPurchaseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectPurchase(
                dbContext.Purchases
                    .AsNoTracking()
                    .Where(purchase => purchase.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<PurchaseDetailItem>.Failure("Purchase not found.")
            : OperationResult<PurchaseDetailItem>.Success(item);
    }

    public async Task<OperationResult<PurchaseDetailItem>> CreatePurchaseAsync(
        CreatePurchaseRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidatePurchaseAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return OperationResult<PurchaseDetailItem>.Failure(validationError);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var purchase = new Purchase(
            GenerateNumber("PUR"),
            request.SupplierId,
            request.PurchaseDate,
            request.SupplierInvoiceNumber,
            request.Notes)
        {
            CreatedBy = performedBy
        };
        foreach (var item in request.Items)
        {
            purchase.AddDetail(
                item.ProductId,
                item.Quantity,
                item.UnitCost,
                item.DiscountAmount,
                item.VatAmount);
        }

        var paymentTotal = request.Payments.Sum(payment => payment.Amount);
        if (paymentTotal > purchase.GrandTotal)
        {
            return OperationResult<PurchaseDetailItem>.Failure(
                "Payments cannot exceed the purchase total.");
        }

        dbContext.Purchases.Add(purchase);
        foreach (var detail in purchase.Details)
        {
            var product = await dbContext.Products.SingleAsync(
                item => item.Id == detail.ProductId,
                cancellationToken);
            await ReceiveStockAsync(
                product,
                detail.Quantity,
                detail.NetUnitCost,
                detail.UnitCost,
                purchase.Id,
                purchase.PurchaseNumber,
                performedBy,
                cancellationToken);
        }

        dbContext.SupplierLedgerEntries.Add(new SupplierLedgerEntry(
            purchase.SupplierId,
            purchase.PurchaseDate,
            SupplierLedgerEntryType.Purchase,
            purchase.GrandTotal,
            0,
            "Purchase",
            purchase.Id,
            purchase.PurchaseNumber,
            performedBy,
            purchase.Notes));

        foreach (var item in request.Payments)
        {
            var payment = new PurchasePayment(
                purchase.Id,
                item.PaymentMethodId,
                item.Amount,
                purchase.PurchaseDate,
                performedBy,
                item.ReferenceNumber,
                item.Notes);
            purchase.AddPayment(payment);
            AddPaymentLedger(purchase, payment, performedBy);
        }

        var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveResult is not null)
        {
            return OperationResult<PurchaseDetailItem>.Failure(saveResult);
        }

        await auditService.WriteAsync(
            "Create",
            "Purchase",
            purchase.Id.ToString(),
            $"Confirmed purchase {purchase.PurchaseNumber}.",
            performedBy,
            cancellationToken);
        return await GetPurchaseAsync(purchase.Id, cancellationToken);
    }

    public async Task<OperationResult<PurchaseDetailItem>> RecordPaymentAsync(
        Guid purchaseId,
        RecordPurchasePaymentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return OperationResult<PurchaseDetailItem>.Failure(
                "Payment amount must be greater than zero.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var purchase = await dbContext.Purchases.SingleOrDefaultAsync(
            item => item.Id == purchaseId,
            cancellationToken);
        if (purchase is null)
        {
            return OperationResult<PurchaseDetailItem>.Failure("Purchase not found.");
        }
        if (!await dbContext.PaymentMethods.AnyAsync(
                method => method.Id == request.PaymentMethodId && method.IsActive,
                cancellationToken))
        {
            return OperationResult<PurchaseDetailItem>.Failure(
                "Payment method is missing or inactive.");
        }
        if (request.Amount > purchase.DueAmount)
        {
            return OperationResult<PurchaseDetailItem>.Failure(
                "Payment cannot exceed the outstanding purchase amount.");
        }

        var payment = new PurchasePayment(
            purchase.Id,
            request.PaymentMethodId,
            request.Amount,
            request.PaidOn,
            performedBy,
            request.ReferenceNumber,
            request.Notes);
        purchase.AddPayment(payment);
        dbContext.PurchasePayments.Add(payment);
        AddPaymentLedger(purchase, payment, performedBy);

        var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveResult is not null)
        {
            return OperationResult<PurchaseDetailItem>.Failure(saveResult);
        }

        await auditService.WriteAsync(
            "Payment",
            "Purchase",
            purchase.Id.ToString(),
            $"Recorded supplier payment for {purchase.PurchaseNumber}.",
            performedBy,
            cancellationToken);
        return await GetPurchaseAsync(purchase.Id, cancellationToken);
    }

    public async Task<OperationResult<PurchaseDetailItem>> CreateReturnAsync(
        Guid purchaseId,
        CreatePurchaseReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0 ||
            request.Items.Any(item => item.Quantity <= 0) ||
            request.Items.GroupBy(item => item.PurchaseDetailId).Any(group => group.Count() > 1))
        {
            return OperationResult<PurchaseDetailItem>.Failure(
                "Add valid, unique purchase lines to the return.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var purchase = await dbContext.Purchases
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == purchaseId, cancellationToken);
        if (purchase is null)
        {
            return OperationResult<PurchaseDetailItem>.Failure("Purchase not found.");
        }

        var purchaseReturn = new PurchaseReturn(
            GenerateNumber("PRN"),
            purchase.Id,
            request.ReturnDate,
            request.Reason,
            performedBy,
            request.Notes);

        foreach (var item in request.Items)
        {
            var detail = purchase.Details.SingleOrDefault(
                line => line.Id == item.PurchaseDetailId);
            if (detail is null)
            {
                return OperationResult<PurchaseDetailItem>.Failure(
                    "A return line does not belong to this purchase.");
            }

            decimal amount;
            try
            {
                amount = detail.RegisterReturn(item.Quantity);
            }
            catch (InvalidOperationException exception)
            {
                return OperationResult<PurchaseDetailItem>.Failure(exception.Message);
            }

            var product = await dbContext.Products.SingleAsync(
                value => value.Id == detail.ProductId,
                cancellationToken);
            var stockError = await ReturnStockAsync(
                product,
                item.Quantity,
                detail.NetUnitCost,
                purchaseReturn.Id,
                purchaseReturn.ReturnNumber,
                performedBy,
                request.Reason,
                cancellationToken);
            if (stockError is not null)
            {
                return OperationResult<PurchaseDetailItem>.Failure(stockError);
            }
            purchaseReturn.AddDetail(detail.Id, item.Quantity, amount);
        }

        if (purchaseReturn.TotalAmount <= 0)
        {
            return OperationResult<PurchaseDetailItem>.Failure(
                "The return must have a positive financial value.");
        }

        purchase.RegisterReturn(purchaseReturn.TotalAmount);
        purchase.Returns.Add(purchaseReturn);
        dbContext.PurchaseReturns.Add(purchaseReturn);
        dbContext.SupplierLedgerEntries.Add(new SupplierLedgerEntry(
            purchase.SupplierId,
            purchaseReturn.ReturnDate,
            SupplierLedgerEntryType.PurchaseReturn,
            0,
            purchaseReturn.TotalAmount,
            "PurchaseReturn",
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            performedBy,
            request.Reason));

        var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveResult is not null)
        {
            return OperationResult<PurchaseDetailItem>.Failure(saveResult);
        }

        await auditService.WriteAsync(
            "Return",
            "Purchase",
            purchase.Id.ToString(),
            $"Recorded {purchaseReturn.ReturnNumber} against {purchase.PurchaseNumber}.",
            performedBy,
            cancellationToken);
        return await GetPurchaseAsync(purchase.Id, cancellationToken);
    }

    public async Task<OperationResult<SupplierLedgerResult>> GetSupplierLedgerAsync(
        Guid supplierId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .Where(item => item.Id == supplierId)
            .Select(item => new { item.Id, item.SupplierCode, item.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (supplier is null)
        {
            return OperationResult<SupplierLedgerResult>.Failure("Supplier not found.");
        }

        var entries = await dbContext.SupplierLedgerEntries
            .AsNoTracking()
            .Where(item => item.SupplierId == supplierId)
            .OrderBy(item => item.EntryDate)
            .ThenBy(item => item.CreatedOn)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var balance = 0m;
        var calculated = entries
            .Select(item =>
            {
                balance += item.Debit - item.Credit;
                return new SupplierLedgerItem(
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
            })
            .ToArray();
        var currentBalance = balance;
        var filtered = calculated
            .Where(item => !from.HasValue || item.EntryDate >= from)
            .Where(item => !to.HasValue || item.EntryDate <= to)
            .OrderByDescending(item => item.EntryDate)
            .ThenByDescending(item => item.Id)
            .ToArray();
        var paged = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return OperationResult<SupplierLedgerResult>.Success(
            new SupplierLedgerResult(
                supplier.Id,
                supplier.SupplierCode,
                supplier.Name,
                currentBalance,
                new PagedResult<SupplierLedgerItem>(
                    paged,
                    page,
                    pageSize,
                    filtered.Length)));
    }

    private async Task<string?> ValidatePurchaseAsync(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return "Add at least one purchase item.";
        }
        if (request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        {
            return "Each product can appear only once in a purchase.";
        }
        if (request.Items.Any(item =>
                item.Quantity <= 0 ||
                item.UnitCost < 0 ||
                item.DiscountAmount < 0 ||
                item.VatAmount < 0 ||
                item.DiscountAmount > item.Quantity * item.UnitCost))
        {
            return "Purchase line quantities and amounts are invalid.";
        }
        if (request.Payments.Any(payment => payment.Amount <= 0))
        {
            return "Payment amounts must be greater than zero.";
        }
        if (!await dbContext.Suppliers.AnyAsync(
                supplier => supplier.Id == request.SupplierId && supplier.IsActive,
                cancellationToken))
        {
            return "Supplier is missing or inactive.";
        }

        var productIds = request.Items.Select(item => item.ProductId).ToArray();
        if (await dbContext.Products.CountAsync(
                product => productIds.Contains(product.Id) && product.IsActive,
                cancellationToken) != productIds.Length)
        {
            return "One or more products are missing or inactive.";
        }
        var paymentMethodIds = request.Payments
            .Select(item => item.PaymentMethodId)
            .Distinct()
            .ToArray();
        if (paymentMethodIds.Length > 0 &&
            await dbContext.PaymentMethods.CountAsync(
                method => paymentMethodIds.Contains(method.Id) && method.IsActive,
                cancellationToken) != paymentMethodIds.Length)
        {
            return "One or more payment methods are missing or inactive.";
        }
        if (!string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber) &&
            await dbContext.Purchases.AnyAsync(
                purchase =>
                    purchase.SupplierId == request.SupplierId &&
                    purchase.SupplierInvoiceNumber == request.SupplierInvoiceNumber.Trim(),
                cancellationToken))
        {
            return "This supplier invoice number has already been used.";
        }

        return null;
    }

    private async Task ReceiveStockAsync(
        Product product,
        decimal quantity,
        decimal inventoryUnitCost,
        decimal latestPurchasePrice,
        Guid referenceId,
        string referenceNumber,
        Guid performedBy,
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
            inventoryUnitCost);
        product.PurchasePrice = decimal.Round(
            latestPurchasePrice,
            2,
            MidpointRounding.AwayFromZero);
        product.LastModifiedBy = performedBy;
        balance.Increase(StockBucket.Available, quantity);
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            StockTransactionType.Purchase,
            StockBucket.Available,
            quantity,
            0,
            balance.AvailableQuantity,
            inventoryUnitCost,
            product.AverageCost,
            "Purchase",
            referenceId,
            performedBy,
            referenceNumber));
    }

    private async Task<string?> ReturnStockAsync(
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
        if (balance is null || balance.AvailableQuantity < quantity)
        {
            return $"Insufficient available stock to return '{product.Name}'.";
        }

        balance.Decrease(StockBucket.Available, quantity);
        product.LastModifiedBy = performedBy;
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            StockTransactionType.PurchaseReturn,
            StockBucket.Available,
            0,
            quantity,
            balance.AvailableQuantity,
            cost,
            product.AverageCost,
            "PurchaseReturn",
            referenceId,
            performedBy,
            $"{referenceNumber}: {reason}"));
        return null;
    }

    private void AddPaymentLedger(
        Purchase purchase,
        PurchasePayment payment,
        Guid performedBy) =>
        dbContext.SupplierLedgerEntries.Add(new SupplierLedgerEntry(
            purchase.SupplierId,
            payment.PaidOn,
            SupplierLedgerEntryType.Payment,
            0,
            payment.Amount,
            "PurchasePayment",
            payment.Id,
            purchase.PurchaseNumber,
            performedBy,
            payment.ReferenceNumber));

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
            return "Stock or supplier data changed concurrently. Reload and try again.";
        }
        catch (DbUpdateException)
        {
            return "The purchase could not be saved because a conflicting record exists.";
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

    private static string GenerateNumber(string prefix) =>
        $"{prefix}-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<PurchaseDetailItem> ProjectPurchase(
        IQueryable<Purchase> query) =>
        query.Select(purchase => new PurchaseDetailItem(
            purchase.Id,
            purchase.PurchaseNumber,
            purchase.SupplierId,
            purchase.Supplier.Name,
            purchase.SupplierInvoiceNumber,
            purchase.PurchaseDate,
            purchase.Status,
            purchase.PaymentStatus,
            purchase.Subtotal,
            purchase.DiscountAmount,
            purchase.VatAmount,
            purchase.GrandTotal,
            purchase.ReturnedAmount,
            purchase.PaidAmount,
            purchase.GrandTotal - purchase.ReturnedAmount - purchase.PaidAmount,
            purchase.Notes,
            purchase.Details
                .OrderBy(item => item.Product.Name)
                .Select(item => new PurchaseLineItem(
                    item.Id,
                    item.ProductId,
                    item.Product.ProductCode,
                    item.Product.Name,
                    item.Product.Unit.Symbol,
                    item.Quantity,
                    item.ReturnedQuantity,
                    item.UnitCost,
                    item.DiscountAmount,
                    item.VatAmount,
                    (item.Quantity * item.UnitCost) -
                        item.DiscountAmount +
                        item.VatAmount))
                .ToArray(),
            purchase.Payments
                .OrderByDescending(item => item.PaidOn)
                .Select(item => new PurchasePaymentItem(
                    item.Id,
                    item.PaymentMethodId,
                    item.PaymentMethod.Name,
                    item.Amount,
                    item.PaidOn,
                    item.ReferenceNumber,
                    item.Notes))
                .ToArray(),
            purchase.Returns
                .OrderByDescending(item => item.ReturnDate)
                .Select(item => new PurchaseReturnItem(
                    item.Id,
                    item.ReturnNumber,
                    item.ReturnDate,
                    item.Reason,
                    item.TotalAmount,
                    item.Notes))
                .ToArray()));
}
