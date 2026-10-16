using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.OnlineOrders;
using RetailShop.Application.Sales;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.OnlineOrders;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.OnlineOrders;

internal sealed class OnlineOrderService(
    RetailShopDbContext dbContext,
    IAuditService auditService,
    ISaleService saleService) : IOnlineOrderService
{
    public async Task<PagedResult<OnlineProductListItem>> GetStoreProductsAsync(
        string? search,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Products.AsNoTracking()
            .Where(item => item.IsActive && item.AllowOnlineSale);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.ILike(item.Name, $"%{term}%") ||
                EF.Functions.ILike(item.ProductCode, $"%{term}%"));
        }
        if (categoryId.HasValue)
        {
            query = query.Where(item => item.CategoryId == categoryId);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new OnlineProductListItem(
                item.Id,
                item.ProductCode,
                item.Name,
                item.Brand == null ? null : item.Brand.Name,
                item.Unit.Symbol,
                item.SalePrice,
                item.Media
                    .Where(media => media.MediaType == RetailShop.Domain.Products.ProductMediaType.Image && media.IsPrimary)
                    .Select(media => "/api/online-store/products/" + item.Id + "/media/" + media.Id + "/content")
                    .FirstOrDefault() ?? item.Images
                    .Where(image => image.IsPrimary)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                dbContext.StockBalances
                    .Where(balance => balance.ProductId == item.Id)
                    .Select(balance => balance.AvailableQuantity)
                    .FirstOrDefault()))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<OnlineProductListItem>(items, page, pageSize, total);
    }

    public async Task<OperationResult<OnlineProductDetailItem>> GetStoreProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.Id == id &&
                product.IsActive &&
                product.AllowOnlineSale)
            .Select(product => new OnlineProductDetailItem(
                product.Id,
                product.ProductCode,
                product.Name,
                product.Brand == null ? null : product.Brand.Name,
                product.CategoryId,
                product.Category.Name,
                product.Unit.Symbol,
                product.Description,
                product.SalePrice,
                product.Media
                    .Where(media => media.MediaType == RetailShop.Domain.Products.ProductMediaType.Image && media.IsPrimary)
                    .Select(media => "/api/online-store/products/" + product.Id + "/media/" + media.Id + "/content")
                    .FirstOrDefault() ?? product.Images
                    .Where(image => image.IsPrimary)
                    .Select(image => image.Url)
                    .FirstOrDefault(),
                dbContext.StockBalances
                    .Where(balance => balance.ProductId == product.Id)
                    .Select(balance => balance.AvailableQuantity)
                    .FirstOrDefault(),
                product.IsWarrantyAvailable,
                product.WarrantyMonths,
                product.Media
                    .OrderBy(media => media.SortOrder)
                    .ThenBy(media => media.CreatedOn)
                    .Select(media => new OnlineProductMediaItem(
                        media.Id,
                        media.MediaType,
                        "/api/online-store/products/" + product.Id + "/media/" + media.Id + "/content",
                        media.ThumbnailStorageKey == null ? null : "/api/online-store/products/" + product.Id + "/media/" + media.Id + "/content?thumbnail=true",
                        media.ContentType,
                        media.FileSize,
                        media.IsPrimary,
                        media.SortOrder))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? OperationResult<OnlineProductDetailItem>.Failure("Product not found.")
            : OperationResult<OnlineProductDetailItem>.Success(item);
    }

    public async Task<OperationResult<IReadOnlyCollection<OnlineProductMediaItem>>> GetStoreProductMediaAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products.AsNoTracking().AnyAsync(product =>
            product.Id == productId && product.IsActive && product.AllowOnlineSale, cancellationToken);
        if (!productExists)
            return OperationResult<IReadOnlyCollection<OnlineProductMediaItem>>.Failure("Product not found.");

        var media = await dbContext.ProductMedia.AsNoTracking()
            .Where(item => item.ProductId == productId)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedOn)
            .Select(item => new OnlineProductMediaItem(
                item.Id,
                item.MediaType,
                "/api/online-store/products/" + productId + "/media/" + item.Id + "/content",
                item.ThumbnailStorageKey == null ? null : "/api/online-store/products/" + productId + "/media/" + item.Id + "/content?thumbnail=true",
                item.ContentType,
                item.FileSize,
                item.IsPrimary,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);
        return OperationResult<IReadOnlyCollection<OnlineProductMediaItem>>.Success(media);
    }

    public async Task<IReadOnlyCollection<OnlineStoreCategoryItem>> GetStoreCategoriesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive && dbContext.Products.Any(product =>
                product.CategoryId == category.Id && product.IsActive && product.AllowOnlineSale))
            .OrderBy(category => category.Name)
            .Select(category => new OnlineStoreCategoryItem(category.Id, category.Name))
            .ToArrayAsync(cancellationToken);

    public async Task<OnlineStorePoliciesItem> GetStorePoliciesAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.InvoiceSettings.AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => new OnlineStorePoliciesItem(item.TermsAndConditions, item.ReturnPolicy))
            .FirstOrDefaultAsync(cancellationToken);
        return settings ?? new OnlineStorePoliciesItem(null, null);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> CreateOrderAsync(
        CreateOnlineOrderRequest request,
        Guid? performedBy,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!IsValidIdempotencyKey(idempotencyKey))
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(
                "The checkout request key is invalid. Start checkout again and retry.");
        }
        var validationError = ValidateOrder(request);
        if (validationError is not null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(validationError);
        }
        var fingerprint = CreateRequestFingerprint(request);
        var existing = await dbContext.OnlineOrders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.IdempotencyKey == idempotencyKey)
            .Select(item => new { item.Id, item.CheckoutFingerprint })
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing.CheckoutFingerprint == fingerprint
                ? await GetOrderAsync(existing.Id, cancellationToken)
                : OperationResult<OnlineOrderDetailItem>.Failure(
                    "This checkout request key was already used with different order information. Start checkout again.");
        }

        var order = new OnlineOrder(
            GenerateNumber("WEB"),
            request.Source,
            request.CustomerName,
            request.CustomerPhone,
            request.DeliveryAddress,
            request.DeliveryCharge,
            request.Notes,
            performedBy,
            idempotencyKey,
            fingerprint);

        foreach (var line in request.Items)
        {
            var product = await dbContext.Products
                .AsNoTracking()
                .Where(item =>
                    item.Id == line.ProductId &&
                    item.IsActive &&
                    item.AllowOnlineSale)
                .Select(item => new
                {
                    item.Id,
                    item.SalePrice,
                    item.AverageCost
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (product is null)
            {
                return OperationResult<OnlineOrderDetailItem>.Failure(
                    "One or more online products are unavailable.");
            }

            // A browser cart is not a reservation. Recheck the current available
            // balance at submission time, while leaving reservation and deduction
            // to the established confirmation/delivery workflow.
            var availableQuantity = await dbContext.StockBalances
                .AsNoTracking()
                .Where(balance => balance.ProductId == product.Id)
                .Select(balance => (decimal?)balance.AvailableQuantity)
                .SingleOrDefaultAsync(cancellationToken) ?? 0;
            if (availableQuantity < line.Quantity)
            {
                return OperationResult<OnlineOrderDetailItem>.Failure(
                    "One or more products no longer have the requested quantity available.");
            }

            order.AddDetail(
                product.Id,
                line.Quantity,
                product.SalePrice,
                product.AverageCost);
        }

        dbContext.OnlineOrders.Add(order);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may win the unique-key race. Return only the
            // order for the identical request, never another constraint failure.
            dbContext.ChangeTracker.Clear();
            var concurrent = await dbContext.OnlineOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.IdempotencyKey == idempotencyKey)
                .Select(item => new { item.Id, item.CheckoutFingerprint })
                .SingleOrDefaultAsync(cancellationToken);
            if (concurrent is not null)
            {
                return concurrent.CheckoutFingerprint == fingerprint
                    ? await GetOrderAsync(concurrent.Id, cancellationToken)
                    : OperationResult<OnlineOrderDetailItem>.Failure(
                        "This checkout request key was already used with different order information. Start checkout again.");
            }
            return OperationResult<OnlineOrderDetailItem>.Failure(
                "The order could not be saved. Please retry checkout.");
        }
        await auditService.WriteAsync(
            "Create",
            "OnlineOrder",
            order.Id.ToString(),
            $"Created online order {order.OrderNumber}.",
            performedBy,
            cancellationToken);
        return await GetOrderAsync(order.Id, cancellationToken);
    }

    public async Task<PagedResult<OnlineOrderListItem>> GetOrdersAsync(
        string? search,
        OnlineOrderStatus? status,
        OnlineOrderSource? source,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.OnlineOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                EF.Functions.ILike(item.OrderNumber, $"%{term}%") ||
                EF.Functions.ILike(item.CustomerName, $"%{term}%") ||
                EF.Functions.ILike(item.CustomerPhone, $"%{term}%"));
        }
        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status);
        }
        if (source.HasValue)
        {
            query = query.Where(item => item.Source == source);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.OrderedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new OnlineOrderListItem(
                item.Id,
                item.OrderNumber,
                item.Source,
                item.Status,
                item.CustomerName,
                item.CustomerPhone,
                item.OrderedOn,
                item.Subtotal + item.DeliveryCharge,
                item.CourierName,
                item.TrackingNumber))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<OnlineOrderListItem>(items, page, pageSize, total);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> GetOrderAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectOrder(
                dbContext.OnlineOrders.AsNoTracking().Where(order => order.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<OnlineOrderDetailItem>.Failure("Online order not found.")
            : OperationResult<OnlineOrderDetailItem>.Success(item);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> ConfirmAsync(
        Guid id,
        ConfirmOnlineOrderRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var order = await LoadOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("Online order not found.");
        }

        try
        {
            foreach (var line in order.Details)
            {
                await ReserveStockAsync(order, line, performedBy, cancellationToken);
            }
            order.Confirm(performedBy, request.Notes);
            dbContext.OnlineOrderHistories.Add(order.History.Last());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(exception.Message);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(saveError);
        }
        await auditService.WriteAsync(
            "Confirm",
            "OnlineOrder",
            order.Id.ToString(),
            $"Confirmed online order {order.OrderNumber}.",
            performedBy,
            cancellationToken);
        return await GetOrderAsync(id, cancellationToken);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> AssignCourierAsync(
        Guid id,
        AssignCourierRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.OnlineOrders
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("Online order not found.");
        }

        try
        {
            order.AssignCourier(
                request.CourierName,
                request.TrackingNumber,
                performedBy,
                request.Notes);
            dbContext.OnlineOrderHistories.Add(order.History.Last());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(exception.Message);
        }

        return await GetOrderAsync(id, cancellationToken);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> CancelAsync(
        Guid id,
        CancelOnlineOrderRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var order = await LoadOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("Online order not found.");
        }

        try
        {
            if (order.Status == OnlineOrderStatus.Confirmed)
            {
                foreach (var line in order.Details)
                {
                    await ReleaseStockAsync(
                        order,
                        line,
                        performedBy,
                        request.Reason,
                        cancellationToken);
                }
            }
            order.Cancel(performedBy, request.Reason);
            dbContext.OnlineOrderHistories.Add(order.History.Last());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(exception.Message);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(saveError);
        }
        return await GetOrderAsync(id, cancellationToken);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> DeliverAsync(
        Guid id,
        DeliverOnlineOrderRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var order = await LoadOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("Online order not found.");
        }

        try
        {
            foreach (var line in order.Details)
            {
                await DeliverStockAsync(order, line, performedBy, cancellationToken);
            }
            order.Deliver(performedBy, request.Notes);
            dbContext.OnlineOrderHistories.Add(order.History.Last());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(exception.Message);
        }

        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(saveError);
        }
        return await GetOrderAsync(id, cancellationToken);
    }

    public async Task<OperationResult<OnlineOrderDetailItem>> PostSaleAsync(
        Guid id,
        PostOnlineOrderSaleRequest request,
        Guid performedBy,
        bool canSellOnDue,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("Online order not found.");
        }
        if (order.Status != OnlineOrderStatus.Delivered)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("Only delivered online orders can be posted as sales.");
        }
        if (order.Sale is not null || await dbContext.Sales.AnyAsync(item => item.OnlineOrderId == id, cancellationToken))
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("This online order is already linked to a sales invoice.");
        }
        if (order.DeliveryCharge != 0)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("This order has a delivery charge. Configure an approved delivery-charge sales treatment before posting it as a sale.");
        }
        if (order.Details.Any(item => item.Product.IsSerialRequired))
        {
            return OperationResult<OnlineOrderDetailItem>.Failure("A delivered order containing serialized products must be reconciled through the existing sale workflow so serial numbers can be assigned.");
        }

        var saleRequest = new CreateSaleRequest(
            request.CustomerId,
            order.DeliveredOn ?? DateTimeOffset.UtcNow,
            $"Online order {order.OrderNumber}",
            order.Details.Select(item => new CreateSaleItemRequest(
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                0,
                0)).ToArray(),
            []);
        var result = await saleService.CreateSaleAsync(
            saleRequest,
            performedBy,
            canChangePrice: true,
            canDiscount: false,
            canSellOnDue: canSellOnDue,
            cancellationToken: cancellationToken,
            onlineOrderId: order.Id,
            stockAlreadyIssued: true);
        if (!result.Succeeded)
        {
            return OperationResult<OnlineOrderDetailItem>.Failure(result.Errors);
        }

        await auditService.WriteAsync(
            "PostSale",
            "OnlineOrder",
            order.Id.ToString(),
            $"Posted delivered online order {order.OrderNumber} as sales invoice {result.Value!.InvoiceNumber}.",
            performedBy,
            cancellationToken);
        return await GetOrderAsync(id, cancellationToken);
    }

    private static string? ValidateOrder(CreateOnlineOrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return "Add at least one product to the order.";
        }
        if (request.Items.Any(item => item.Quantity <= 0) ||
            request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        {
            return "Order lines must be positive and unique.";
        }
        if (request.DeliveryCharge < 0)
        {
            return "Delivery charge cannot be negative.";
        }
        return null;
    }

    private static bool IsValidIdempotencyKey(string key) =>
        key.Length is >= 16 and <= 128 && key.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static string CreateRequestFingerprint(CreateOnlineOrderRequest request)
    {
        var canonical = new StringBuilder();
        canonical.Append((int)request.Source).Append('|')
            .Append(request.CustomerName.Trim()).Append('|')
            .Append(request.CustomerPhone.Trim()).Append('|')
            .Append(request.DeliveryAddress.Trim()).Append('|')
            .Append(request.DeliveryCharge.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
            .Append(request.Notes?.Trim() ?? string.Empty);
        foreach (var line in request.Items.OrderBy(item => item.ProductId))
        {
            canonical.Append('|').Append(line.ProductId.ToString("N"))
                .Append(':').Append(line.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private async Task<OnlineOrder?> LoadOrderAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await dbContext.OnlineOrders
            .Include(item => item.Details)
                .ThenInclude(item => item.Product)
            .Include(item => item.History)
            .Include(item => item.Sale)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    private async Task ReserveStockAsync(
        OnlineOrder order,
        OnlineOrderDetail line,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var balance = await GetBalanceAsync(line.ProductId, cancellationToken);
        balance.Reserve(line.Quantity);
        dbContext.StockTransactions.Add(new StockTransaction(
            line.ProductId,
            StockTransactionType.OnlineReservation,
            StockBucket.Available,
            0,
            line.Quantity,
            balance.AvailableQuantity,
            line.CostPrice,
            line.Product.AverageCost,
            "OnlineOrder",
            order.Id,
            performedBy,
            order.OrderNumber));
        dbContext.StockTransactions.Add(new StockTransaction(
            line.ProductId,
            StockTransactionType.OnlineReservation,
            StockBucket.Reserved,
            line.Quantity,
            0,
            balance.ReservedQuantity,
            line.CostPrice,
            line.Product.AverageCost,
            "OnlineOrder",
            order.Id,
            performedBy,
            order.OrderNumber));
    }

    private async Task ReleaseStockAsync(
        OnlineOrder order,
        OnlineOrderDetail line,
        Guid performedBy,
        string reason,
        CancellationToken cancellationToken)
    {
        var balance = await GetBalanceAsync(line.ProductId, cancellationToken);
        balance.ReleaseReservation(line.Quantity);
        dbContext.StockTransactions.Add(new StockTransaction(
            line.ProductId,
            StockTransactionType.ReservationRelease,
            StockBucket.Reserved,
            0,
            line.Quantity,
            balance.ReservedQuantity,
            line.CostPrice,
            line.Product.AverageCost,
            "OnlineOrder",
            order.Id,
            performedBy,
            $"{order.OrderNumber}: {reason}"));
        dbContext.StockTransactions.Add(new StockTransaction(
            line.ProductId,
            StockTransactionType.ReservationRelease,
            StockBucket.Available,
            line.Quantity,
            0,
            balance.AvailableQuantity,
            line.CostPrice,
            line.Product.AverageCost,
            "OnlineOrder",
            order.Id,
            performedBy,
            $"{order.OrderNumber}: {reason}"));
    }

    private async Task DeliverStockAsync(
        OnlineOrder order,
        OnlineOrderDetail line,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var balance = await GetBalanceAsync(line.ProductId, cancellationToken);
        balance.Decrease(StockBucket.Reserved, line.Quantity);
        dbContext.StockTransactions.Add(new StockTransaction(
            line.ProductId,
            StockTransactionType.OnlineDelivery,
            StockBucket.Reserved,
            0,
            line.Quantity,
            balance.ReservedQuantity,
            line.CostPrice,
            line.Product.AverageCost,
            "OnlineOrder",
            order.Id,
            performedBy,
            order.OrderNumber));
    }

    private async Task<StockBalance> GetBalanceAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.StockBalances.SingleOrDefaultAsync(
            item => item.ProductId == productId,
            cancellationToken);
        if (balance is null)
        {
            throw new InvalidOperationException("Stock balance was not found.");
        }

        return balance;
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
            return "Online order stock changed concurrently. Reload and try again.";
        }
        catch (DbUpdateException)
        {
            return "The online order could not be saved because a conflicting record exists.";
        }
    }

    private static string GenerateNumber(string prefix) =>
        $"{prefix}-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<OnlineOrderDetailItem> ProjectOrder(
        IQueryable<OnlineOrder> query) =>
        query.Select(order => new OnlineOrderDetailItem(
            order.Id,
            order.OrderNumber,
            order.Source,
            order.Status,
            order.CustomerName,
            order.CustomerPhone,
            order.DeliveryAddress,
            order.Subtotal,
            order.DeliveryCharge,
            order.Subtotal + order.DeliveryCharge,
            order.CourierName,
            order.TrackingNumber,
            order.Notes,
            order.OrderedOn,
            order.ConfirmedOn,
            order.CancelledOn,
            order.DeliveredOn,
            order.Sale == null ? null : order.Sale.Id,
            order.Sale == null ? null : order.Sale.InvoiceNumber,
            order.Sale == null ? 0 : order.Sale.PaidAmount,
            order.Sale == null || order.Sale.Status == RetailShop.Domain.Sales.SaleStatus.Cancelled
                ? 0
                : order.Sale.GrandTotal - order.Sale.ReturnedAmount - (order.Sale.PaidAmount - order.Sale.RefundedAmount),
            order.Sale == null ? null : order.Sale.PaymentStatus,
            order.Details
                .OrderBy(item => item.Product.Name)
                .Select(item => new OnlineOrderLineItem(
                    item.Id,
                    item.ProductId,
                    item.Product.ProductCode,
                    item.Product.Name,
                    item.Product.Unit.Symbol,
                    item.Quantity,
                    item.UnitPrice,
                    item.Quantity * item.UnitPrice))
                .ToArray(),
            order.History
                .OrderBy(item => item.PerformedOn)
                .Select(item => new OnlineOrderHistoryItem(
                    item.Id,
                    item.Action,
                    item.PerformedBy,
                    item.PerformedOn,
                    item.Notes))
                .ToArray()));
}
