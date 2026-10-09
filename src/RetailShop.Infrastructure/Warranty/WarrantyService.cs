using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Warranty;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Warranty;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Warranty;

internal sealed class WarrantyService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IWarrantyService
{
    public async Task<IReadOnlyCollection<ProductSerialItem>> RegisterSerialsAsync(
        RegisterProductSerialsRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var serials = request.SerialNumbers
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (serials.Length == 0)
        {
            return [];
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            item => item.Id == request.ProductId && item.IsActive,
            cancellationToken);
        if (product is null)
        {
            return [];
        }

        var normalized = serials
            .Select(ProductSerial.Normalize)
            .ToArray();
        var existing = await dbContext.ProductSerials
            .Where(item => normalized.Contains(item.NormalizedSerialNumber))
            .Select(item => item.NormalizedSerialNumber)
            .ToArrayAsync(cancellationToken);
        var toCreate = serials
            .Where(item => !existing.Contains(
                ProductSerial.Normalize(item),
                StringComparer.OrdinalIgnoreCase))
            .Select(item => new ProductSerial(
                request.ProductId,
                item,
                request.PurchaseId,
                request.PurchaseDetailId,
                performedBy))
            .ToArray();

        if (toCreate.Length > 0)
        {
            dbContext.ProductSerials.AddRange(toCreate);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(
                "Register",
                "ProductSerial",
                product.Id.ToString(),
                $"Registered {toCreate.Length} serial number(s) for {product.Name}.",
                performedBy,
                cancellationToken);
        }

        return await ProjectSerials(
                dbContext.ProductSerials.AsNoTracking()
                    .Where(item => normalized.Contains(item.NormalizedSerialNumber)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProductSerialItem>> GetAvailableSerialsAsync(
        Guid productId,
        CancellationToken cancellationToken) =>
        await ProjectSerials(
                dbContext.ProductSerials.AsNoTracking()
                    .Where(item =>
                        item.ProductId == productId &&
                        item.Status == ProductSerialStatus.Available))
            .OrderBy(item => item.SerialNumber)
            .ToArrayAsync(cancellationToken);

    public async Task<OperationResult<WarrantyLookupItem>> LookupBySerialAsync(
        string serialNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return OperationResult<WarrantyLookupItem>.Failure(
                "Enter a serial number.");
        }

        var serial = await ProjectSerials(
                dbContext.ProductSerials.AsNoTracking()
                    .Where(item => item.NormalizedSerialNumber ==
                        ProductSerial.Normalize(serialNumber)))
            .SingleOrDefaultAsync(cancellationToken);
        if (serial is null)
        {
            return OperationResult<WarrantyLookupItem>.Failure(
                "Serial number was not found.");
        }

        var claims = await ProjectClaimList(
                dbContext.WarrantyClaims.AsNoTracking()
                    .Where(item => item.ProductSerial.SerialNumber == serial.SerialNumber))
            .ToArrayAsync(cancellationToken);
        return OperationResult<WarrantyLookupItem>.Success(
            new WarrantyLookupItem(serial, claims));
    }

    public async Task<OperationResult<IReadOnlyCollection<WarrantyLookupItem>>>
        LookupByInvoiceAsync(
            string invoiceNumber,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return OperationResult<IReadOnlyCollection<WarrantyLookupItem>>.Failure(
                "Enter an invoice number.");
        }

        var sale = await dbContext.Sales
            .AsNoTracking()
            .Where(item => item.InvoiceNumber == invoiceNumber.Trim())
            .Select(item => new { item.Id })
            .SingleOrDefaultAsync(cancellationToken);
        if (sale is null)
        {
            return OperationResult<IReadOnlyCollection<WarrantyLookupItem>>.Failure(
                "Invoice was not found.");
        }

        var serials = await ProjectSerials(
                dbContext.ProductSerials.AsNoTracking()
                    .Where(item => item.SaleId == sale.Id))
            .ToArrayAsync(cancellationToken);
        var results = new List<WarrantyLookupItem>();
        foreach (var serial in serials)
        {
            var claims = await ProjectClaimList(
                    dbContext.WarrantyClaims.AsNoTracking()
                        .Where(item => item.ProductSerialId == serial.Id))
                .ToArrayAsync(cancellationToken);
            results.Add(new WarrantyLookupItem(serial, claims));
        }

        return OperationResult<IReadOnlyCollection<WarrantyLookupItem>>.Success(results);
    }

    public async Task<PagedResult<WarrantyClaimListItem>> GetClaimsAsync(
        string? search,
        WarrantyClaimStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.WarrantyClaims.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.ILike(item.ClaimNumber, $"%{term}%") ||
                EF.Functions.ILike(item.ProductSerial.SerialNumber, $"%{term}%") ||
                EF.Functions.ILike(item.ProductSerial.Product.Name, $"%{term}%") ||
                EF.Functions.ILike(item.Sale.InvoiceNumber, $"%{term}%") ||
                (item.Customer != null &&
                 EF.Functions.ILike(item.Customer.Name, $"%{term}%")));
        }
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectClaimList(query
            .OrderByDescending(item => item.RequestedOn))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<WarrantyClaimListItem>(items, page, pageSize, total);
    }

    public async Task<OperationResult<WarrantyClaimDetailItem>> GetClaimAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectClaimDetail(
                dbContext.WarrantyClaims.AsNoTracking().Where(claim => claim.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<WarrantyClaimDetailItem>.Failure(
                "Warranty claim not found.")
            : OperationResult<WarrantyClaimDetailItem>.Success(item);
    }

    public async Task<OperationResult<WarrantyClaimDetailItem>> CreateClaimAsync(
        CreateWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Complaint))
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Complaint is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var serial = await dbContext.ProductSerials
            .Include(item => item.Product)
            .SingleOrDefaultAsync(
                item => item.NormalizedSerialNumber ==
                    ProductSerial.Normalize(request.SerialNumber),
                cancellationToken);
        if (serial is null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Serial number was not found.");
        }
        if (serial.SaleId is null || serial.SaleDetailId is null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Warranty claims are allowed only for sold serial numbers.");
        }
        if (!serial.Product.IsWarrantyAvailable)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "This product does not have warranty enabled.");
        }
        if (!serial.IsWarrantyActive(DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "The warranty period has expired or has not started.");
        }
        if (await dbContext.WarrantyClaims.AnyAsync(
                item =>
                    item.ProductSerialId == serial.Id &&
                    item.Status != WarrantyClaimStatus.Rejected &&
                    item.Status != WarrantyClaimStatus.Resolved &&
                    item.Status != WarrantyClaimStatus.Replaced &&
                    item.Status != WarrantyClaimStatus.Refunded,
                cancellationToken))
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "This serial already has an active warranty claim.");
        }

        var claim = new WarrantyClaim(
            GenerateNumber("WCL"),
            serial.Id,
            serial.SaleId.Value,
            serial.CustomerId,
            request.Complaint,
            request.RequestedAction,
            performedBy,
            request.Notes);
        dbContext.WarrantyClaims.Add(claim);
        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Create",
            "WarrantyClaim",
            claim.Id.ToString(),
            $"Created warranty claim {claim.ClaimNumber} for serial {serial.SerialNumber}.",
            performedBy,
            cancellationToken);
        return await GetClaimAsync(claim.Id, cancellationToken);
    }

    public async Task<OperationResult<WarrantyClaimDetailItem>> ApproveAsync(
        Guid id,
        ReviewWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var claim = await dbContext.WarrantyClaims
            .Include(item => item.ProductSerial)
                .ThenInclude(item => item.Product)
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (claim is null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Warranty claim not found.");
        }

        try
        {
            claim.Approve(performedBy, request.Notes);
            claim.ProductSerial.MoveToWarranty(performedBy);
            await MoveStockAsync(
                claim.ProductSerial.Product,
                StockBucket.Warranty,
                StockTransactionType.WarrantyClaim,
                claim.Id,
                claim.ClaimNumber,
                performedBy,
                request.Notes,
                cancellationToken);
            dbContext.WarrantyClaimHistories.Add(claim.History.Last());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(exception.Message);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Approve",
            "WarrantyClaim",
            claim.Id.ToString(),
            $"Approved warranty claim {claim.ClaimNumber}.",
            performedBy,
            cancellationToken);
        return await GetClaimAsync(id, cancellationToken);
    }

    public async Task<OperationResult<WarrantyClaimDetailItem>> RejectAsync(
        Guid id,
        RejectWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var claim = await dbContext.WarrantyClaims
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (claim is null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Warranty claim not found.");
        }

        try
        {
            claim.Reject(performedBy, request.Reason);
            dbContext.WarrantyClaimHistories.Add(claim.History.Last());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(exception.Message);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Reject",
            "WarrantyClaim",
            claim.Id.ToString(),
            $"Rejected warranty claim {claim.ClaimNumber}.",
            performedBy,
            cancellationToken);
        return await GetClaimAsync(id, cancellationToken);
    }

    public async Task<OperationResult<WarrantyClaimDetailItem>> ResolveAsync(
        Guid id,
        ResolveWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var claim = await dbContext.WarrantyClaims
            .Include(item => item.ProductSerial)
                .ThenInclude(item => item.Product)
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (claim is null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Warranty claim not found.");
        }
        if (claim.Status == WarrantyClaimStatus.Pending)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(
                "Approve or reject the claim before resolution.");
        }

        try
        {
            claim.Resolve(request.Action, performedBy, request.Notes);
            await ApplyResolutionStockAsync(
                claim,
                request,
                performedBy,
                cancellationToken);
            dbContext.WarrantyClaimHistories.Add(claim.History.Last());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(exception.Message);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<WarrantyClaimDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Resolve",
            "WarrantyClaim",
            claim.Id.ToString(),
            $"Resolved warranty claim {claim.ClaimNumber} as {claim.Status}.",
            performedBy,
            cancellationToken);
        return await GetClaimAsync(id, cancellationToken);
    }

    private async Task ApplyResolutionStockAsync(
        WarrantyClaim claim,
        ResolveWarrantyClaimRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        switch (request.Action)
        {
            case WarrantyResolutionAction.SupplierClaim:
                await TransferBucketAsync(
                    claim.ProductSerial.Product,
                    StockBucket.Warranty,
                    StockBucket.SupplierClaim,
                    StockTransactionType.SupplierClaim,
                    claim.Id,
                    claim.ClaimNumber,
                    performedBy,
                    request.Notes,
                    cancellationToken);
                claim.ProductSerial.MoveToSupplierClaim(performedBy);
                break;
            case WarrantyResolutionAction.Repair:
                claim.ProductSerial.MarkRepaired(performedBy);
                break;
            case WarrantyResolutionAction.Replacement:
                await IssueReplacementAsync(
                    claim,
                    request.ReplacementSerialNumber,
                    performedBy,
                    request.Notes,
                    cancellationToken);
                claim.ProductSerial.MarkReplaced(performedBy);
                break;
            case WarrantyResolutionAction.Refund:
                claim.ProductSerial.MarkRetired(performedBy);
                break;
            case WarrantyResolutionAction.Resolve:
                claim.ProductSerial.MarkRepaired(performedBy);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Action));
        }
    }

    private async Task IssueReplacementAsync(
        WarrantyClaim claim,
        string? replacementSerialNumber,
        Guid performedBy,
        string? notes,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.ProductId == claim.ProductSerial.ProductId,
            cancellationToken);
        if (balance is null || balance.AvailableQuantity < 1)
        {
            throw new InvalidOperationException(
                "Replacement stock is not available.");
        }

        balance.Decrease(StockBucket.Available, 1);
        dbContext.StockTransactions.Add(new StockTransaction(
            claim.ProductSerial.ProductId,
            StockTransactionType.WarrantyReplacement,
            StockBucket.Available,
            0,
            1,
            balance.AvailableQuantity,
            claim.ProductSerial.Product.AverageCost,
            claim.ProductSerial.Product.AverageCost,
            "WarrantyClaim",
            claim.Id,
            performedBy,
            notes ?? claim.ClaimNumber));

        if (!string.IsNullOrWhiteSpace(replacementSerialNumber))
        {
            var replacement = await dbContext.ProductSerials.SingleOrDefaultAsync(
                item =>
                    item.ProductId == claim.ProductSerial.ProductId &&
                    item.NormalizedSerialNumber ==
                        ProductSerial.Normalize(replacementSerialNumber),
                cancellationToken);
            if (replacement is null)
            {
                throw new InvalidOperationException(
                    "Replacement serial number was not found.");
            }
            replacement.MarkSold(
                claim.SaleId,
                claim.ProductSerial.SaleDetailId!.Value,
                claim.CustomerId,
                DateTimeOffset.UtcNow,
                claim.ProductSerial.Product.WarrantyMonths,
                performedBy);
        }
    }

    private async Task MoveStockAsync(
        Domain.Products.Product product,
        StockBucket bucket,
        StockTransactionType transactionType,
        Guid referenceId,
        string referenceNumber,
        Guid performedBy,
        string? notes,
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

        balance.Increase(bucket, 1);
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            transactionType,
            bucket,
            1,
            0,
            balance.GetQuantity(bucket),
            product.AverageCost,
            product.AverageCost,
            "WarrantyClaim",
            referenceId,
            performedBy,
            notes ?? referenceNumber));
    }

    private async Task TransferBucketAsync(
        Domain.Products.Product product,
        StockBucket from,
        StockBucket to,
        StockTransactionType transactionType,
        Guid referenceId,
        string referenceNumber,
        Guid performedBy,
        string? notes,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.ProductId == product.Id,
            cancellationToken);
        if (balance is null || balance.GetQuantity(from) < 1)
        {
            throw new InvalidOperationException(
                $"No stock is available in {from} for this warranty claim.");
        }

        balance.Decrease(from, 1);
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            transactionType,
            from,
            0,
            1,
            balance.GetQuantity(from),
            product.AverageCost,
            product.AverageCost,
            "WarrantyClaim",
            referenceId,
            performedBy,
            notes ?? referenceNumber));
        balance.Increase(to, 1);
        dbContext.StockTransactions.Add(new StockTransaction(
            product.Id,
            transactionType,
            to,
            1,
            0,
            balance.GetQuantity(to),
            product.AverageCost,
            product.AverageCost,
            "WarrantyClaim",
            referenceId,
            performedBy,
            notes ?? referenceNumber));
    }

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
            return "Warranty or stock data changed concurrently. Reload and try again.";
        }
        catch (DbUpdateException)
        {
            return "The warranty operation could not be saved because a conflicting record exists.";
        }
    }

    private static string GenerateNumber(string prefix) =>
        $"{prefix}-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<ProductSerialItem> ProjectSerials(
        IQueryable<ProductSerial> query) =>
        query.Select(item => new ProductSerialItem(
            item.Id,
            item.ProductId,
            item.Product.ProductCode,
            item.Product.Name,
            item.SerialNumber,
            item.Status,
            item.SaleId,
            item.Sale == null ? null : item.Sale.InvoiceNumber,
            item.Customer == null ? null : item.Customer.Name,
            item.WarrantyStartDate,
            item.WarrantyExpiryDate,
            item.WarrantyStartDate != null &&
            item.WarrantyExpiryDate != null &&
            item.WarrantyStartDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
            item.WarrantyExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow)));

    private static IQueryable<WarrantyClaimListItem> ProjectClaimList(
        IQueryable<WarrantyClaim> query) =>
        query.Select(item => new WarrantyClaimListItem(
            item.Id,
            item.ClaimNumber,
            item.ProductSerial.SerialNumber,
            item.ProductSerial.Product.ProductCode,
            item.ProductSerial.Product.Name,
            item.Sale.InvoiceNumber,
            item.Customer == null ? null : item.Customer.Name,
            item.Status,
            item.RequestedAction,
            item.RequestedOn));

    private static IQueryable<WarrantyClaimDetailItem> ProjectClaimDetail(
        IQueryable<WarrantyClaim> query) =>
        query.Select(item => new WarrantyClaimDetailItem(
            item.Id,
            item.ClaimNumber,
            new ProductSerialItem(
                item.ProductSerial.Id,
                item.ProductSerial.ProductId,
                item.ProductSerial.Product.ProductCode,
                item.ProductSerial.Product.Name,
                item.ProductSerial.SerialNumber,
                item.ProductSerial.Status,
                item.ProductSerial.SaleId,
                item.ProductSerial.Sale == null ? null : item.ProductSerial.Sale.InvoiceNumber,
                item.ProductSerial.Customer == null ? null : item.ProductSerial.Customer.Name,
                item.ProductSerial.WarrantyStartDate,
                item.ProductSerial.WarrantyExpiryDate,
                item.ProductSerial.WarrantyStartDate != null &&
                item.ProductSerial.WarrantyExpiryDate != null &&
                item.ProductSerial.WarrantyStartDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                item.ProductSerial.WarrantyExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow)),
            item.Sale.InvoiceNumber,
            item.Customer == null ? null : item.Customer.Name,
            item.Complaint,
            item.RequestedAction,
            item.Status,
            item.RequestedOn,
            item.ReviewedOn,
            item.ResolvedOn,
            item.Notes,
            item.ReviewNotes,
            item.History
                .OrderBy(history => history.PerformedOn)
                .Select(history => new WarrantyClaimHistoryItem(
                    history.Id,
                    history.Action,
                    history.PerformedBy,
                    history.PerformedOn,
                    history.Notes))
                .ToArray()));
}
