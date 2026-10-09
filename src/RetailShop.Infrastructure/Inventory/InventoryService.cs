using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Inventory;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Products;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Inventory;

internal sealed class InventoryService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IInventoryService
{
    public async Task<OperationResult<OpeningStockResult>> RecordOpeningStockAsync(
        OpeningStockRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return OperationResult<OpeningStockResult>.Failure(
                "Add at least one opening-stock item.");
        }

        if (request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        {
            return OperationResult<OpeningStockResult>.Failure(
                "Each product can appear only once in an opening-stock batch.");
        }

        if (request.Items.Any(item => item.Quantity <= 0 || item.UnitCost < 0))
        {
            return OperationResult<OpeningStockResult>.Failure(
                "Quantities must be greater than zero and costs cannot be negative.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var batchId = Guid.CreateVersion7();

        foreach (var item in request.Items)
        {
            var product = await dbContext.Products.SingleOrDefaultAsync(
                product => product.Id == item.ProductId && product.IsActive,
                cancellationToken);
            if (product is null)
            {
                return OperationResult<OpeningStockResult>.Failure(
                    "One or more products are missing or inactive.");
            }

            if (await dbContext.StockTransactions.AnyAsync(
                    stock => stock.ProductId == item.ProductId,
                    cancellationToken))
            {
                return OperationResult<OpeningStockResult>.Failure(
                    $"Opening stock has already been recorded for '{product.Name}'.");
            }

            var balance = await GetOrCreateBalanceAsync(
                item.ProductId,
                cancellationToken);
            if (balance.TotalQuantity != 0)
            {
                return OperationResult<OpeningStockResult>.Failure(
                    $"'{product.Name}' already has a stock balance.");
            }

            product.AverageCost = RoundCost(item.UnitCost);
            product.LastModifiedBy = performedBy;
            balance.Increase(StockBucket.Available, item.Quantity);
            AddTransaction(
                product,
                balance,
                StockTransactionType.Opening,
                StockBucket.Available,
                item.Quantity,
                0,
                item.UnitCost,
                "OpeningStock",
                batchId,
                performedBy,
                request.Remarks);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<OpeningStockResult>.Failure(
                "Stock changed concurrently. Reload and try again.");
        }

        await auditService.WriteAsync(
            "Create",
            "OpeningStock",
            batchId.ToString(),
            $"Recorded opening stock for {request.Items.Count} product(s).",
            performedBy,
            cancellationToken);

        return OperationResult<OpeningStockResult>.Success(
            new OpeningStockResult(batchId, DateTimeOffset.UtcNow, request.Items.Count));
    }

    public async Task<PagedResult<CurrentStockItem>> GetCurrentStockAsync(
        string? search,
        bool lowStockOnly,
        bool damagedOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from product in dbContext.Products.AsNoTracking()
            join stockBalance in dbContext.StockBalances.AsNoTracking()
                on product.Id equals stockBalance.ProductId into balances
            from balance in balances.DefaultIfEmpty()
            select new { product, balance };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.ILike(item.product.Name, $"%{term}%") ||
                EF.Functions.ILike(item.product.ProductCode, $"%{term}%") ||
                item.product.Barcodes.Any(barcode =>
                    EF.Functions.ILike(barcode.Value, $"%{term}%")));
        }

        if (lowStockOnly)
        {
            query = query.Where(item =>
                item.balance == null ||
                item.balance.AvailableQuantity <= item.product.MinimumStockLevel);
        }

        if (damagedOnly)
        {
            query = query.Where(item =>
                item.balance != null && item.balance.DamagedQuantity > 0);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.product.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CurrentStockItem(
                item.product.Id,
                item.product.ProductCode,
                item.product.Barcodes
                    .Where(barcode => barcode.IsPrimary)
                    .Select(barcode => barcode.Value)
                    .FirstOrDefault() ?? string.Empty,
                item.product.Name,
                item.product.Category.Name,
                item.product.Unit.Symbol,
                item.balance == null ? 0 : item.balance.AvailableQuantity,
                item.balance == null ? 0 : item.balance.ReservedQuantity,
                item.balance == null ? 0 : item.balance.DamagedQuantity,
                item.balance == null ? 0 : item.balance.WarrantyQuantity,
                item.balance == null ? 0 : item.balance.SupplierClaimQuantity,
                item.balance == null ? 0 : item.balance.TotalQuantity,
                item.product.MinimumStockLevel,
                item.product.AverageCost,
                (item.balance == null ? 0 : item.balance.TotalQuantity) *
                    item.product.AverageCost,
                (item.balance == null ? 0 : item.balance.AvailableQuantity) <=
                    item.product.MinimumStockLevel))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<CurrentStockItem>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<StockLedgerItem>> GetLedgerAsync(
        Guid? productId,
        StockTransactionType? transactionType,
        StockBucket? bucket,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.StockTransactions.AsNoTracking();

        if (productId.HasValue)
        {
            query = query.Where(item => item.ProductId == productId);
        }
        if (transactionType.HasValue)
        {
            query = query.Where(item => item.TransactionType == transactionType);
        }
        if (bucket.HasValue)
        {
            query = query.Where(item => item.Bucket == bucket);
        }
        if (from.HasValue)
        {
            query = query.Where(item => item.TransactionDate >= from);
        }
        if (to.HasValue)
        {
            query = query.Where(item => item.TransactionDate <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new StockLedgerItem(
                item.Id,
                item.TransactionDate,
                item.ProductId,
                item.Product.ProductCode,
                item.Product.Name,
                item.TransactionType,
                item.Bucket,
                item.QuantityIn,
                item.QuantityOut,
                item.BalanceQuantity,
                item.CostPrice,
                item.AverageCostAfterTransaction,
                item.ReferenceType,
                item.ReferenceId,
                item.Remarks))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<StockLedgerItem>(items, page, pageSize, totalCount);
    }

    public async Task<OperationResult<StockAdjustmentDetailItem>> CreateAdjustmentAsync(
        CreateStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var errors = await ValidateAdjustmentAsync(request, cancellationToken);
        if (errors.Count > 0)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(errors);
        }

        var adjustment = new StockAdjustment(
            await GenerateAdjustmentNumberAsync(cancellationToken),
            request.Reason,
            performedBy)
        {
            Notes = Clean(request.Notes)
        };
        foreach (var item in request.Items)
        {
            adjustment.Details.Add(new StockAdjustmentDetail(
                item.ProductId,
                item.Bucket,
                item.Direction,
                item.Quantity,
                item.UnitCost));
        }

        dbContext.StockAdjustments.Add(adjustment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Request",
            "StockAdjustment",
            adjustment.Id.ToString(),
            $"Requested stock adjustment {adjustment.AdjustmentNumber}.",
            performedBy,
            cancellationToken);
        return await GetAdjustmentAsync(adjustment.Id, cancellationToken);
    }

    public async Task<PagedResult<StockAdjustmentDetailItem>> GetAdjustmentsAsync(
        StockAdjustmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.StockAdjustments.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(adjustment => adjustment.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageQuery = query
            .OrderByDescending(adjustment => adjustment.RequestedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        var items = await ProjectAdjustments(pageQuery)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<StockAdjustmentDetailItem>(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<OperationResult<StockAdjustmentDetailItem>> GetAdjustmentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectAdjustments(
                dbContext.StockAdjustments
                    .AsNoTracking()
                    .Where(adjustment => adjustment.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<StockAdjustmentDetailItem>.Failure(
                "Stock adjustment not found.")
            : OperationResult<StockAdjustmentDetailItem>.Success(item);
    }

    public async Task<OperationResult<StockAdjustmentDetailItem>> ApproveAdjustmentAsync(
        Guid id,
        ReviewStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var adjustment = await dbContext.StockAdjustments
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Stock adjustment not found.");
        }
        if (adjustment.Status != StockAdjustmentStatus.Pending)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Only a pending adjustment can be approved.");
        }
        foreach (var detail in adjustment.Details)
        {
            var product = await dbContext.Products.SingleAsync(
                item => item.Id == detail.ProductId,
                cancellationToken);
            var balance = await GetOrCreateBalanceAsync(
                detail.ProductId,
                cancellationToken);
            var result = ApplyAdjustment(
                adjustment,
                detail,
                product,
                balance,
                performedBy,
                reverse: false);
            if (result is not null)
            {
                return OperationResult<StockAdjustmentDetailItem>.Failure(result);
            }
        }

        adjustment.Approve(performedBy, request.Notes);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Stock changed concurrently. Reload and try again.");
        }

        await auditService.WriteAsync(
            "Approve",
            "StockAdjustment",
            adjustment.Id.ToString(),
            $"Approved stock adjustment {adjustment.AdjustmentNumber}.",
            performedBy,
            cancellationToken);
        return await GetAdjustmentAsync(id, cancellationToken);
    }

    public async Task<OperationResult<StockAdjustmentDetailItem>> RejectAdjustmentAsync(
        Guid id,
        RejectStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var adjustment = await dbContext.StockAdjustments.FindAsync(
            [id],
            cancellationToken);
        if (adjustment is null)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Stock adjustment not found.");
        }
        if (adjustment.Status != StockAdjustmentStatus.Pending)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Only a pending adjustment can be rejected.");
        }

        adjustment.Reject(performedBy, request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Reject",
            "StockAdjustment",
            adjustment.Id.ToString(),
            $"Rejected stock adjustment {adjustment.AdjustmentNumber}.",
            performedBy,
            cancellationToken);
        return await GetAdjustmentAsync(id, cancellationToken);
    }

    public async Task<OperationResult<StockAdjustmentDetailItem>> ReverseAdjustmentAsync(
        Guid id,
        ReverseStockAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var adjustment = await dbContext.StockAdjustments
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Stock adjustment not found.");
        }
        if (adjustment.Status != StockAdjustmentStatus.Approved)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Only an approved adjustment can be reversed.");
        }

        adjustment.Reverse(performedBy, request.Reason);
        foreach (var detail in adjustment.Details)
        {
            var product = await dbContext.Products.SingleAsync(
                item => item.Id == detail.ProductId,
                cancellationToken);
            var balance = await dbContext.StockBalances.SingleAsync(
                item => item.ProductId == detail.ProductId,
                cancellationToken);
            var result = ApplyAdjustment(
                adjustment,
                detail,
                product,
                balance,
                performedBy,
                reverse: true);
            if (result is not null)
            {
                return OperationResult<StockAdjustmentDetailItem>.Failure(result);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<StockAdjustmentDetailItem>.Failure(
                "Stock changed concurrently. Reload and try again.");
        }

        await auditService.WriteAsync(
            "Reverse",
            "StockAdjustment",
            adjustment.Id.ToString(),
            $"Reversed stock adjustment {adjustment.AdjustmentNumber}.",
            performedBy,
            cancellationToken);
        return await GetAdjustmentAsync(id, cancellationToken);
    }

    private string? ApplyAdjustment(
        StockAdjustment adjustment,
        StockAdjustmentDetail detail,
        Product product,
        StockBalance balance,
        Guid performedBy,
        bool reverse)
    {
        var effectiveDirection = reverse
            ? detail.Direction == StockAdjustmentDirection.Increase
                ? StockAdjustmentDirection.Decrease
                : StockAdjustmentDirection.Increase
            : detail.Direction;
        var cost = reverse
            ? detail.AppliedCost
            : detail.UnitCost > 0
                ? detail.UnitCost
                : product.AverageCost;

        if (effectiveDirection == StockAdjustmentDirection.Decrease &&
            balance.GetQuantity(detail.Bucket) < detail.Quantity)
        {
            return $"Insufficient {detail.Bucket.ToString().ToLowerInvariant()} " +
                   $"stock for '{product.Name}'.";
        }

        if (detail.Bucket == StockBucket.Available &&
            effectiveDirection == StockAdjustmentDirection.Increase)
        {
            product.AverageCost = CalculateWeightedAverage(
                balance.AvailableQuantity + balance.ReservedQuantity,
                product.AverageCost,
                detail.Quantity,
                cost);
        }
        else if (reverse &&
                 detail.Bucket == StockBucket.Available &&
                 effectiveDirection == StockAdjustmentDirection.Decrease)
        {
            product.AverageCost = CalculateAverageAfterReversingReceipt(
                balance.AvailableQuantity + balance.ReservedQuantity,
                product.AverageCost,
                detail.Quantity,
                cost);
        }

        if (effectiveDirection == StockAdjustmentDirection.Increase)
        {
            balance.Increase(detail.Bucket, detail.Quantity);
        }
        else
        {
            balance.Decrease(detail.Bucket, detail.Quantity);
        }

        product.LastModifiedBy = performedBy;
        if (!reverse)
        {
            detail.AppliedCost = RoundCost(cost);
        }

        var transactionType = (effectiveDirection, reverse) switch
        {
            (StockAdjustmentDirection.Increase, false) =>
                StockTransactionType.AdjustmentIn,
            (StockAdjustmentDirection.Decrease, false) =>
                StockTransactionType.AdjustmentOut,
            (StockAdjustmentDirection.Increase, true) =>
                StockTransactionType.AdjustmentReversalIn,
            _ => StockTransactionType.AdjustmentReversalOut
        };
        AddTransaction(
            product,
            balance,
            transactionType,
            detail.Bucket,
            effectiveDirection == StockAdjustmentDirection.Increase
                ? detail.Quantity
                : 0,
            effectiveDirection == StockAdjustmentDirection.Decrease
                ? detail.Quantity
                : 0,
            cost,
            reverse ? "StockAdjustmentReversal" : "StockAdjustment",
            adjustment.Id,
            performedBy,
            reverse ? adjustment.ReversalReason : adjustment.Reason);
        return null;
    }

    private async Task<List<string>> ValidateAdjustmentAsync(
        CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("Adjustment reason is required.");
        }
        if (request.Items.Count == 0)
        {
            errors.Add("Add at least one adjustment item.");
        }
        if (request.Items.Any(item =>
                item.Quantity <= 0 ||
                item.UnitCost < 0 ||
                item.Bucket == StockBucket.Reserved ||
                !Enum.IsDefined(item.Bucket) ||
                !Enum.IsDefined(item.Direction)))
        {
            errors.Add(
                "Adjustment items contain an invalid quantity, cost, bucket, or direction.");
        }
        if (request.Items
            .GroupBy(item => new { item.ProductId, item.Bucket })
            .Any(group => group.Count() > 1))
        {
            errors.Add("A product and stock bucket can appear only once per request.");
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToArray();
        var activeProductCount = await dbContext.Products.CountAsync(
            product => productIds.Contains(product.Id) && product.IsActive,
            cancellationToken);
        if (activeProductCount != productIds.Length)
        {
            errors.Add("One or more products are missing or inactive.");
        }
        return errors;
    }

    private async Task<StockBalance> GetOrCreateBalanceAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.ProductId == productId,
            cancellationToken);
        if (balance is not null)
        {
            return balance;
        }

        balance = new StockBalance(productId);
        dbContext.StockBalances.Add(balance);
        return balance;
    }

    private void AddTransaction(
        Product product,
        StockBalance balance,
        StockTransactionType transactionType,
        StockBucket bucket,
        decimal quantityIn,
        decimal quantityOut,
        decimal cost,
        string referenceType,
        Guid referenceId,
        Guid performedBy,
        string? remarks) =>
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            transactionType,
            bucket,
            quantityIn,
            quantityOut,
            balance.GetQuantity(bucket),
            RoundCost(cost),
            RoundCost(product.AverageCost),
            referenceType,
            referenceId,
            performedBy,
            remarks));

    private async Task<string> GenerateAdjustmentNumberAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var number = $"ADJ-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            if (!await dbContext.StockAdjustments.AnyAsync(
                    item => item.AdjustmentNumber == number,
                    cancellationToken))
            {
                return number;
            }
        }

        throw new InvalidOperationException(
            "Unable to generate a unique adjustment number.");
    }

    private static IQueryable<StockAdjustmentDetailItem> ProjectAdjustments(
        IQueryable<StockAdjustment> query) =>
        query.Select(adjustment => new StockAdjustmentDetailItem(
            adjustment.Id,
            adjustment.AdjustmentNumber,
            adjustment.RequestedOn,
            adjustment.Reason,
            adjustment.Notes,
            adjustment.Status,
            adjustment.RequestedBy,
            adjustment.ReviewedBy,
            adjustment.ReviewedOn,
            adjustment.ReviewNotes,
            adjustment.ReversedBy,
            adjustment.ReversedOn,
            adjustment.ReversalReason,
            adjustment.Details
                .OrderBy(detail => detail.Product.Name)
                .Select(detail => new StockAdjustmentLineItem(
                    detail.Id,
                    detail.ProductId,
                    detail.Product.ProductCode,
                    detail.Product.Name,
                    detail.Product.Unit.Symbol,
                    detail.Bucket,
                    detail.Direction,
                    detail.Quantity,
                    detail.UnitCost,
                    detail.AppliedCost))
                .ToArray()));

    private static decimal CalculateWeightedAverage(
        decimal existingQuantity,
        decimal existingAverage,
        decimal addedQuantity,
        decimal addedCost)
    {
        var newQuantity = existingQuantity + addedQuantity;
        return newQuantity == 0
            ? 0
            : RoundCost(
                (existingQuantity * existingAverage + addedQuantity * addedCost) /
                newQuantity);
    }

    private static decimal CalculateAverageAfterReversingReceipt(
        decimal existingQuantity,
        decimal existingAverage,
        decimal removedQuantity,
        decimal removedCost)
    {
        var remainingQuantity = existingQuantity - removedQuantity;
        if (remainingQuantity <= 0)
        {
            return 0;
        }

        var remainingValue =
            existingQuantity * existingAverage - removedQuantity * removedCost;
        return RoundCost(Math.Max(0, remainingValue / remainingQuantity));
    }

    private static decimal RoundCost(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
