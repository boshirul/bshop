using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Returns;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Returns;
using RetailShop.Domain.Sales;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Returns;

internal sealed class ReturnService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IReturnService
{
    public async Task<IReadOnlyCollection<ComplaintReasonItem>>
        GetComplaintReasonsAsync(CancellationToken cancellationToken) =>
        await dbContext.ComplaintReasons
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .Select(item => new ComplaintReasonItem(item.Id, item.Name))
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<ReturnInvoiceItem>> GetInvoiceAsync(
        string invoiceNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return OperationResult<ReturnInvoiceItem>.Failure(
                "An original invoice number is required.");
        }

        var sale = await dbContext.Sales
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Details)
                .ThenInclude(item => item.Product)
                    .ThenInclude(item => item.Unit)
            .SingleOrDefaultAsync(
                item => item.InvoiceNumber == invoiceNumber.Trim(),
                cancellationToken);
        if (sale is null || sale.Status != SaleStatus.Completed)
        {
            return OperationResult<ReturnInvoiceItem>.Failure(
                "A completed sale with that invoice number was not found.");
        }

        var returned = await dbContext.SalesReturnDetails
            .Where(item =>
                item.SalesReturn.SaleId == sale.Id &&
                item.SalesReturn.Status != SalesReturnStatus.Rejected)
            .GroupBy(item => item.SaleDetailId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity, cancellationToken);

        return OperationResult<ReturnInvoiceItem>.Success(new ReturnInvoiceItem(
            sale.Id,
            sale.InvoiceNumber,
            sale.CustomerId,
            sale.Customer?.Name ?? "Walk-in customer",
            sale.SaleDate,
            sale.GrandTotal,
            sale.PaidAmount - sale.RefundedAmount,
            sale.DueAmount,
            sale.Details
                .OrderBy(item => item.Product.Name)
                .Select(item =>
                {
                    returned.TryGetValue(item.Id, out var returnedQuantity);
                    return new ReturnInvoiceLineItem(
                        item.Id,
                        item.ProductId,
                        item.Product.ProductCode,
                        item.Product.Name,
                        item.Product.Unit.Symbol,
                        item.Quantity,
                        returnedQuantity,
                        Math.Max(0, item.Quantity - returnedQuantity),
                        decimal.Round(item.LineTotal / item.Quantity, 2));
                })
                .ToArray()));
    }

    public async Task<PagedResult<SalesReturnListItem>> GetReturnsAsync(
        string? search,
        SalesReturnStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.SalesReturns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.ILike(item.ReturnNumber, $"%{term}%") ||
                EF.Functions.ILike(item.Sale.InvoiceNumber, $"%{term}%") ||
                (item.Sale.Customer != null &&
                 EF.Functions.ILike(item.Sale.Customer.Name, $"%{term}%")));
        }
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.RequestedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SalesReturnListItem(
                item.Id,
                item.ReturnNumber,
                item.Sale.InvoiceNumber,
                item.Sale.Customer == null
                    ? "Walk-in customer"
                    : item.Sale.Customer.Name,
                item.ComplaintReason.Name,
                item.RequestedAction,
                item.ProductCondition,
                item.Status,
                item.TotalAmount,
                item.RequestedOn))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<SalesReturnListItem>(
            items, page, pageSize, totalCount);
    }

    public async Task<OperationResult<SalesReturnDetailItem>> GetReturnAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectReturn(
                dbContext.SalesReturns.AsNoTracking().Where(x => x.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<SalesReturnDetailItem>.Failure("Return not found.")
            : OperationResult<SalesReturnDetailItem>.Success(item);
    }

    public async Task<OperationResult<SalesReturnDetailItem>> CreateReturnAsync(
        CreateSalesReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber) ||
            request.Items.Count == 0 ||
            request.Items.Any(item => item.Quantity <= 0) ||
            request.Items.GroupBy(item => item.SaleDetailId).Any(x => x.Count() > 1))
        {
            return OperationResult<SalesReturnDetailItem>.Failure(
                "Invoice, unique return items, and positive quantities are required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var sale = await dbContext.Sales
            .Include(item => item.Details)
            .SingleOrDefaultAsync(
                item => item.InvoiceNumber == request.InvoiceNumber.Trim(),
                cancellationToken);
        if (sale is null || sale.Status != SaleStatus.Completed)
        {
            return OperationResult<SalesReturnDetailItem>.Failure(
                "A completed original sales invoice is required.");
        }
        if (!await dbContext.ComplaintReasons.AnyAsync(
                item => item.Id == request.ComplaintReasonId && item.IsActive,
                cancellationToken))
        {
            return OperationResult<SalesReturnDetailItem>.Failure(
                "Select an active complaint reason.");
        }

        var requestedIds = request.Items.Select(item => item.SaleDetailId).ToArray();
        if (sale.Details.Count(item => requestedIds.Contains(item.Id)) != requestedIds.Length)
        {
            return OperationResult<SalesReturnDetailItem>.Failure(
                "Every returned product must belong to the original invoice.");
        }
        var previous = await dbContext.SalesReturnDetails
            .Where(item =>
                requestedIds.Contains(item.SaleDetailId) &&
                item.SalesReturn.Status != SalesReturnStatus.Rejected)
            .GroupBy(item => item.SaleDetailId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(item => item.Id, item => item.Quantity, cancellationToken);

        var result = new SalesReturn(
            GenerateReturnNumber(),
            sale.Id,
            request.ComplaintReasonId,
            request.ProductCondition,
            request.RequestedAction,
            performedBy,
            request.Notes);
        foreach (var requested in request.Items)
        {
            var sold = sale.Details.Single(item => item.Id == requested.SaleDetailId);
            previous.TryGetValue(sold.Id, out var alreadyReturned);
            if (requested.Quantity > sold.Quantity - alreadyReturned)
            {
                return OperationResult<SalesReturnDetailItem>.Failure(
                    $"Return quantity for an item exceeds the remaining sold quantity.");
            }
            var amount = decimal.Round(
                sold.LineTotal / sold.Quantity * requested.Quantity,
                2,
                MidpointRounding.AwayFromZero);
            result.AddDetail(sold.Id, requested.Quantity, amount);
        }

        dbContext.SalesReturns.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await auditService.WriteAsync(
            "Request",
            "SalesReturn",
            result.Id.ToString(),
            $"Submitted return {result.ReturnNumber} for {sale.InvoiceNumber}.",
            performedBy,
            cancellationToken);
        return await GetReturnAsync(result.Id, cancellationToken);
    }

    public async Task<OperationResult<SalesReturnDetailItem>> ApproveAsync(
        Guid id,
        ReviewSalesReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var item = await dbContext.SalesReturns
            .Include(x => x.Sale)
            .Include(x => x.Details)
                .ThenInclude(x => x.SaleDetail)
                    .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return OperationResult<SalesReturnDetailItem>.Failure("Return not found.");
        }

        var dueAdjusted = 0m;
        var refunded = 0m;
        if (item.RequestedAction == SalesReturnAction.DueAdjustment)
        {
            if (!item.Sale.CustomerId.HasValue || item.TotalAmount > item.Sale.DueAmount)
            {
                return OperationResult<SalesReturnDetailItem>.Failure(
                    "Due adjustment requires a customer with enough invoice due.");
            }
            dueAdjusted = item.TotalAmount;
        }
        else if (item.RequestedAction == SalesReturnAction.Refund)
        {
            dueAdjusted = Math.Min(item.TotalAmount, item.Sale.DueAmount);
            refunded = item.TotalAmount - dueAdjusted;
            if (refunded > 0 &&
                (!request.PaymentMethodId.HasValue ||
                 !await dbContext.PaymentMethods.AnyAsync(
                     x => x.Id == request.PaymentMethodId && x.IsActive,
                     cancellationToken)))
            {
                return OperationResult<SalesReturnDetailItem>.Failure(
                    "Select an active payment method for the cash refund.");
            }
        }

        try
        {
            item.Resolve(
                performedBy,
                refunded,
                dueAdjusted,
                refunded > 0 ? request.PaymentMethodId : null,
                request.Notes);
            dbContext.ReturnApprovals.Add(item.History.Single());
            if (item.RequestedAction != SalesReturnAction.Replacement)
            {
                item.Sale.ApplyReturn(item.TotalAmount, refunded);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<SalesReturnDetailItem>.Failure(exception.Message);
        }

        foreach (var detail in item.Details)
        {
            var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
                x => x.ProductId == detail.SaleDetail.ProductId,
                cancellationToken);
            if (balance is null)
            {
                balance = new StockBalance(detail.SaleDetail.ProductId);
                dbContext.StockBalances.Add(balance);
            }
            var bucket = ToBucket(item.ProductCondition);
            balance.Increase(bucket, detail.Quantity);
            AddStockTransaction(
                detail,
                item,
                bucket,
                detail.Quantity,
                0,
                balance.GetQuantity(bucket),
                performedBy,
                "Returned product received.");

            if (item.RequestedAction == SalesReturnAction.Replacement)
            {
                if (balance.AvailableQuantity < detail.Quantity)
                {
                    return OperationResult<SalesReturnDetailItem>.Failure(
                        $"Insufficient available stock to replace '{detail.SaleDetail.Product.Name}'.");
                }
                balance.Decrease(StockBucket.Available, detail.Quantity);
                AddStockTransaction(
                    detail,
                    item,
                    StockBucket.Available,
                    0,
                    detail.Quantity,
                    balance.AvailableQuantity,
                    performedBy,
                    "Replacement issued.");
            }
        }

        if (item.Sale.CustomerId.HasValue &&
            item.RequestedAction != SalesReturnAction.Replacement)
        {
            AddLedger(item, CustomerLedgerEntryType.Refund, 0, item.TotalAmount,
                performedBy, "Sales return credit.");
            if (refunded > 0)
            {
                AddLedger(item, CustomerLedgerEntryType.Refund, refunded, 0,
                    performedBy, "Refund paid to customer.");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await auditService.WriteAsync(
            "Approve",
            "SalesReturn",
            item.Id.ToString(),
            $"Approved {item.ReturnNumber} as {item.Status}.",
            performedBy,
            cancellationToken);
        return await GetReturnAsync(item.Id, cancellationToken);
    }

    public async Task<OperationResult<SalesReturnDetailItem>> RejectAsync(
        Guid id,
        RejectSalesReturnRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.SalesReturns.SingleOrDefaultAsync(
            x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return OperationResult<SalesReturnDetailItem>.Failure("Return not found.");
        }
        try
        {
            item.Reject(performedBy, request.Notes);
            dbContext.ReturnApprovals.Add(item.History.Single());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<SalesReturnDetailItem>.Failure(exception.Message);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Reject", "SalesReturn", item.Id.ToString(),
            $"Rejected {item.ReturnNumber}: {request.Notes.Trim()}",
            performedBy, cancellationToken);
        return await GetReturnAsync(item.Id, cancellationToken);
    }

    private void AddStockTransaction(
        SalesReturnDetail detail,
        SalesReturn item,
        StockBucket bucket,
        decimal quantityIn,
        decimal quantityOut,
        decimal balance,
        Guid performedBy,
        string remarks) =>
        dbContext.StockTransactions.Add(new StockTransaction(
            detail.SaleDetail.ProductId,
            StockTransactionType.SalesReturn,
            bucket,
            quantityIn,
            quantityOut,
            balance,
            detail.SaleDetail.CostPrice,
            detail.SaleDetail.Product.AverageCost,
            "SalesReturn",
            item.Id,
            performedBy,
            $"{item.ReturnNumber}: {remarks}"));

    private void AddLedger(
        SalesReturn item,
        CustomerLedgerEntryType type,
        decimal debit,
        decimal credit,
        Guid performedBy,
        string notes) =>
        dbContext.CustomerLedgerEntries.Add(new CustomerLedgerEntry(
            item.Sale.CustomerId!.Value,
            item.ReviewedOn!.Value,
            type,
            debit,
            credit,
            "SalesReturn",
            item.Id,
            item.ReturnNumber,
            performedBy,
            notes));

    private static StockBucket ToBucket(ReturnProductCondition condition) =>
        condition switch
        {
            ReturnProductCondition.Available => StockBucket.Available,
            ReturnProductCondition.Damaged => StockBucket.Damaged,
            ReturnProductCondition.Warranty => StockBucket.Warranty,
            ReturnProductCondition.SupplierClaim => StockBucket.SupplierClaim,
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };

    private static string GenerateReturnNumber() =>
        $"RET-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<SalesReturnDetailItem> ProjectReturn(
        IQueryable<SalesReturn> query) =>
        query.Select(item => new SalesReturnDetailItem(
            item.Id,
            item.ReturnNumber,
            item.SaleId,
            item.Sale.InvoiceNumber,
            item.Sale.CustomerId,
            item.Sale.Customer == null ? "Walk-in customer" : item.Sale.Customer.Name,
            item.ComplaintReason.Name,
            item.ProductCondition,
            item.RequestedAction,
            item.Status,
            item.TotalAmount,
            item.RefundedAmount,
            item.DueAdjustedAmount,
            item.PaymentMethod == null ? null : item.PaymentMethod.Name,
            item.Notes,
            item.ReviewNotes,
            item.RequestedBy,
            item.RequestedOn,
            item.ReviewedBy,
            item.ReviewedOn,
            item.Details
                .OrderBy(x => x.SaleDetail.Product.Name)
                .Select(x => new SalesReturnLineItem(
                    x.Id,
                    x.SaleDetailId,
                    x.SaleDetail.ProductId,
                    x.SaleDetail.Product.ProductCode,
                    x.SaleDetail.Product.Name,
                    x.SaleDetail.Product.Unit.Symbol,
                    x.Quantity,
                    x.Amount))
                .ToArray(),
            item.History
                .OrderBy(x => x.PerformedOn)
                .Select(x => new ReturnApprovalItem(
                    x.Action, x.PerformedBy, x.PerformedOn, x.Notes))
                .ToArray()));
}
