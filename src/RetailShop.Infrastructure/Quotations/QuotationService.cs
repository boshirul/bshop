using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Quotations;
using RetailShop.Application.Sales;
using RetailShop.Domain.Inventory;
using RetailShop.Domain.Products;
using RetailShop.Domain.Quotations;
using RetailShop.Domain.Sales;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Quotations;

internal sealed class QuotationService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : IQuotationService
{
    public async Task<PagedResult<QuotationListItem>> GetAsync(
        string? search,
        QuotationStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await ExpireStaleAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Quotations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(quotation =>
                EF.Functions.ILike(quotation.QuotationNumber, $"%{term}%") ||
                EF.Functions.ILike(quotation.Customer.Name, $"%{term}%") ||
                EF.Functions.ILike(quotation.Customer.Phone, $"%{term}%"));
        }
        if (status.HasValue)
        {
            query = query.Where(quotation => quotation.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(quotation => quotation.QuotationDate)
            .ThenByDescending(quotation => quotation.QuotationNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(quotation => new QuotationListItem(
                quotation.Id,
                quotation.QuotationNumber,
                quotation.CustomerId,
                quotation.Customer.Name,
                quotation.QuotationDate,
                quotation.ValidUntil,
                quotation.Status,
                quotation.GrandTotal,
                quotation.ConvertedSaleId))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<QuotationListItem>(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<OperationResult<QuotationDetailItem>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await ExpireStaleAsync(cancellationToken);
        var item = await ProjectQuotation(
                dbContext.Quotations.AsNoTracking().Where(value => value.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<QuotationDetailItem>.Failure("Quotation not found.")
            : OperationResult<QuotationDetailItem>.Success(item);
    }

    public async Task<OperationResult<QuotationDetailItem>> CreateAsync(
        SaveQuotationRequest request,
        Guid performedBy,
        bool canChangePrice,
        bool canDiscount,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(
            request,
            canChangePrice,
            canDiscount,
            cancellationToken);
        if (error is not null)
        {
            return OperationResult<QuotationDetailItem>.Failure(error);
        }

        var quotation = new Quotation(
            GenerateNumber(),
            request.CustomerId,
            request.QuotationDate,
            request.ValidUntil,
            request.Notes,
            request.Terms)
        {
            CreatedBy = performedBy
        };
        foreach (var item in request.Items)
        {
            quotation.AddDetail(
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount,
                item.VatAmount);
        }
        dbContext.Quotations.Add(quotation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Create",
            "Quotation",
            quotation.Id.ToString(),
            $"Created quotation {quotation.QuotationNumber}.",
            performedBy,
            cancellationToken);
        return await GetByIdAsync(quotation.Id, cancellationToken);
    }

    public async Task<OperationResult<QuotationDetailItem>> UpdateAsync(
        Guid id,
        SaveQuotationRequest request,
        Guid performedBy,
        bool canChangePrice,
        bool canDiscount,
        CancellationToken cancellationToken)
    {
        var error = await ValidateAsync(
            request,
            canChangePrice,
            canDiscount,
            cancellationToken);
        if (error is not null)
        {
            return OperationResult<QuotationDetailItem>.Failure(error);
        }

        var quotation = await dbContext.Quotations
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quotation is null)
        {
            return OperationResult<QuotationDetailItem>.Failure("Quotation not found.");
        }

        try
        {
            quotation.Update(
                request.CustomerId,
                request.QuotationDate,
                request.ValidUntil,
                request.Notes,
                request.Terms);
            dbContext.QuotationDetails.RemoveRange(quotation.Details);
            quotation.ReplaceDetails([]);
            foreach (var item in request.Items)
            {
                quotation.AddDetail(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.VatAmount);
            }
            dbContext.QuotationDetails.AddRange(quotation.Details);
        }
        catch (InvalidOperationException exception)
        {
            return OperationResult<QuotationDetailItem>.Failure(exception.Message);
        }

        quotation.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Update",
            "Quotation",
            quotation.Id.ToString(),
            $"Updated quotation {quotation.QuotationNumber}.",
            performedBy,
            cancellationToken);
        return await GetByIdAsync(quotation.Id, cancellationToken);
    }

    public Task<OperationResult<QuotationDetailItem>> SendAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            id,
            quotation => quotation.Send(),
            "Send",
            performedBy,
            cancellationToken);

    public Task<OperationResult<QuotationDetailItem>> AcceptAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            id,
            quotation => quotation.Accept(),
            "Accept",
            performedBy,
            cancellationToken);

    public Task<OperationResult<QuotationDetailItem>> RejectAsync(
        Guid id,
        RejectQuotationRequest request,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            id,
            quotation => quotation.Reject(request.Reason),
            "Reject",
            performedBy,
            cancellationToken);

    public async Task<OperationResult<SaleDetailItem>> ConvertAsync(
        Guid id,
        ConvertQuotationRequest request,
        Guid performedBy,
        bool canSellOnDue,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var quotation = await dbContext.Quotations
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quotation is null)
        {
            return OperationResult<SaleDetailItem>.Failure("Quotation not found.");
        }
        if (quotation.Status != QuotationStatus.Accepted)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Only an accepted quotation can be converted.");
        }
        if (quotation.ValidUntil.Date < DateTimeOffset.UtcNow.Date)
        {
            quotation.Expire();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<SaleDetailItem>.Failure(
                "The quotation has expired.");
        }

        var paymentTotal = request.Payments.Sum(payment => payment.Amount);
        if (request.Payments.Any(payment => payment.Amount <= 0) ||
            paymentTotal > quotation.GrandTotal)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "Quotation conversion payments are invalid.");
        }
        var dueAmount = quotation.GrandTotal - paymentTotal;
        if (dueAmount > 0 && !canSellOnDue)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "You do not have permission to convert this quotation with a due balance.");
        }

        var customer = await dbContext.Customers.SingleOrDefaultAsync(
            item => item.Id == quotation.CustomerId && item.IsActive,
            cancellationToken);
        if (customer is null)
        {
            return OperationResult<SaleDetailItem>.Failure(
                "The quotation customer is missing or inactive.");
        }
        var existingBalance = await dbContext.CustomerLedgerEntries
            .Where(entry => entry.CustomerId == customer.Id)
            .SumAsync(
                entry => (decimal?)(entry.Debit - entry.Credit),
                cancellationToken) ?? 0;
        if (existingBalance + dueAmount > customer.CreditLimit)
        {
            return OperationResult<SaleDetailItem>.Failure(
                $"Conversion exceeds the customer's credit limit of {customer.CreditLimit:0.00}.");
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
            quotation.CustomerId,
            request.SaleDate,
            request.Notes ?? $"Converted from {quotation.QuotationNumber}")
        {
            CreatedBy = performedBy
        };
        foreach (var line in quotation.Details)
        {
            var product = await dbContext.Products.SingleOrDefaultAsync(
                item => item.Id == line.ProductId && item.IsActive,
                cancellationToken);
            if (product is null)
            {
                return OperationResult<SaleDetailItem>.Failure(
                    "One or more quoted products are missing or inactive.");
            }
            sale.AddDetail(
                line.ProductId,
                line.Quantity,
                line.UnitPrice,
                line.DiscountAmount,
                line.VatAmount,
                product.AverageCost);
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
        }

        AddCustomerLedger(
            customer.Id,
            sale.SaleDate,
            CustomerLedgerEntryType.Sale,
            sale.GrandTotal,
            0,
            "Sale",
            sale.Id,
            sale.InvoiceNumber,
            performedBy,
            $"Converted from {quotation.QuotationNumber}");
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
            AddCustomerLedger(
                customer.Id,
                payment.PaidOn,
                CustomerLedgerEntryType.Payment,
                0,
                payment.Amount,
                "SalePayment",
                payment.Id,
                sale.InvoiceNumber,
                performedBy,
                payment.ReferenceNumber);
        }

        quotation.Convert(sale.Id);
        quotation.LastModifiedBy = performedBy;
        var saveError = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveError is not null)
        {
            return OperationResult<SaleDetailItem>.Failure(saveError);
        }

        await auditService.WriteAsync(
            "Convert",
            "Quotation",
            quotation.Id.ToString(),
            $"Converted {quotation.QuotationNumber} to {sale.InvoiceNumber}.",
            performedBy,
            cancellationToken);
        return await GetSaleAsync(sale.Id, cancellationToken);
    }

    private async Task<OperationResult<QuotationDetailItem>> ChangeStatusAsync(
        Guid id,
        Action<Quotation> change,
        string action,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var quotation = await dbContext.Quotations
            .Include(item => item.Details)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (quotation is null)
        {
            return OperationResult<QuotationDetailItem>.Failure("Quotation not found.");
        }

        try
        {
            change(quotation);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            if (quotation.Status == QuotationStatus.Expired)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return OperationResult<QuotationDetailItem>.Failure(exception.Message);
        }

        quotation.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            action,
            "Quotation",
            quotation.Id.ToString(),
            $"{action} quotation {quotation.QuotationNumber}.",
            performedBy,
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<string?> ValidateAsync(
        SaveQuotationRequest request,
        bool canChangePrice,
        bool canDiscount,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return "Add at least one quotation item.";
        }
        if (request.ValidUntil.Date < request.QuotationDate.Date)
        {
            return "Quotation validity cannot end before its issue date.";
        }
        if (request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        {
            return "Each product can appear only once in a quotation.";
        }
        if (request.Items.Any(item =>
                item.Quantity <= 0 ||
                item.UnitPrice < 0 ||
                item.DiscountAmount < 0 ||
                item.VatAmount < 0 ||
                item.DiscountAmount > item.Quantity * item.UnitPrice))
        {
            return "Quotation line quantities and amounts are invalid.";
        }
        if (!canDiscount && request.Items.Any(item => item.DiscountAmount > 0))
        {
            return "You do not have permission to apply discounts.";
        }
        if (!await dbContext.Customers.AnyAsync(
                item => item.Id == request.CustomerId && item.IsActive,
                cancellationToken))
        {
            return "Customer is missing or inactive.";
        }

        var productIds = request.Items.Select(item => item.ProductId).ToArray();
        var products = await dbContext.Products
            .Where(item => productIds.Contains(item.Id) && item.IsActive)
            .Select(item => new { item.Id, item.Name, item.SalePrice })
            .ToArrayAsync(cancellationToken);
        if (products.Length != productIds.Length)
        {
            return "One or more products are missing or inactive.";
        }
        if (!canChangePrice)
        {
            foreach (var line in request.Items)
            {
                var product = products.Single(item => item.Id == line.ProductId);
                if (decimal.Round(line.UnitPrice, 2) !=
                    decimal.Round(product.SalePrice, 2))
                {
                    return $"You do not have permission to change the price of '{product.Name}'.";
                }
            }
        }
        return null;
    }

    private async Task ExpireStaleAsync(CancellationToken cancellationToken)
    {
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var stale = await dbContext.Quotations
            .Where(item =>
                item.ValidUntil < today &&
                (item.Status == QuotationStatus.Draft ||
                 item.Status == QuotationStatus.Sent ||
                 item.Status == QuotationStatus.Accepted))
            .ToArrayAsync(cancellationToken);
        foreach (var quotation in stale)
        {
            quotation.Expire();
        }
        if (stale.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
            "QuotationConversion",
            referenceId,
            performedBy,
            referenceNumber));
        return null;
    }

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
            return "The quotation could not be converted because a conflicting record exists.";
        }
    }

    private async Task<OperationResult<SaleDetailItem>> GetSaleAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sale = await dbContext.Sales
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new SaleDetailItem(
                item.Id,
                item.InvoiceNumber,
                item.CustomerId,
                item.Customer == null ? "Walk-in customer" : item.Customer.Name,
                item.Customer == null ? null : item.Customer.Phone,
                item.SaleDate,
                item.Status,
                item.PaymentStatus,
                item.Subtotal,
                item.DiscountAmount,
                item.VatAmount,
                item.GrandTotal,
                item.PaidAmount,
                item.GrandTotal - item.PaidAmount,
                item.Details.Sum(line =>
                    ((line.Quantity * line.UnitPrice) -
                     line.DiscountAmount +
                     line.VatAmount) -
                    (line.Quantity * line.CostPrice)),
                item.Notes,
                item.CancellationReason,
                item.CancelledOn,
                item.Details.Select(line => new SaleLineItem(
                    line.Id,
                    line.ProductId,
                    line.Product.ProductCode,
                    line.Product.Name,
                    line.Product.Unit.Symbol,
                    line.Quantity,
                    line.UnitPrice,
                    line.DiscountAmount,
                    line.VatAmount,
                    (line.Quantity * line.UnitPrice) -
                        line.DiscountAmount +
                        line.VatAmount,
                    line.CostPrice,
                    ((line.Quantity * line.UnitPrice) -
                     line.DiscountAmount +
                     line.VatAmount) -
                        (line.Quantity * line.CostPrice))).ToArray(),
                item.Payments.Select(payment => new SalePaymentItem(
                    payment.Id,
                    payment.PaymentMethodId,
                    payment.PaymentMethod.Name,
                    payment.Amount,
                    payment.PaidOn,
                    payment.ReferenceNumber,
                    payment.Notes)).ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
        return sale is null
            ? OperationResult<SaleDetailItem>.Failure("Converted sale not found.")
            : OperationResult<SaleDetailItem>.Success(sale);
    }

    private static string GenerateNumber() =>
        $"QUO-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static string GenerateFallbackInvoiceNumber() =>
        $"INV-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<QuotationDetailItem> ProjectQuotation(
        IQueryable<Quotation> query) =>
        query.Select(quotation => new QuotationDetailItem(
            quotation.Id,
            quotation.QuotationNumber,
            quotation.CustomerId,
            quotation.Customer.Name,
            quotation.Customer.Phone,
            quotation.QuotationDate,
            quotation.ValidUntil,
            quotation.Status,
            quotation.Subtotal,
            quotation.DiscountAmount,
            quotation.VatAmount,
            quotation.GrandTotal,
            quotation.Notes,
            quotation.Terms,
            quotation.RejectionReason,
            quotation.SentOn,
            quotation.AcceptedOn,
            quotation.RejectedOn,
            quotation.ConvertedOn,
            quotation.ConvertedSaleId,
            quotation.Details
                .OrderBy(detail => detail.Product.Name)
                .Select(detail => new QuotationLineItem(
                    detail.Id,
                    detail.ProductId,
                    detail.Product.ProductCode,
                    detail.Product.Name,
                    detail.Product.Unit.Symbol,
                    detail.Quantity,
                    detail.UnitPrice,
                    detail.DiscountAmount,
                    detail.VatAmount,
                    (detail.Quantity * detail.UnitPrice) -
                        detail.DiscountAmount +
                        detail.VatAmount))
                .ToArray()));
}
