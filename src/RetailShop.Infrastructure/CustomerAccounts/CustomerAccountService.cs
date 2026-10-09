using System.Data;
using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.CustomerAccounts;
using RetailShop.Domain.CustomerAccounts;
using RetailShop.Domain.Sales;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.CustomerAccounts;

internal sealed class CustomerAccountService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : ICustomerAccountService
{
    public async Task<PagedResult<CustomerDueSummaryItem>> GetDuesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var balances = await dbContext.CustomerLedgerEntries
            .AsNoTracking()
            .GroupBy(entry => entry.CustomerId)
            .Select(ledger => new
            {
                CustomerId = ledger.Key,
                Balance =
                    ledger.Sum(item => item.Debit) -
                    ledger.Sum(item => item.Credit)
            })
            .Where(item => item.Balance > 0)
            .ToArrayAsync(cancellationToken);
        var balanceByCustomer = balances.ToDictionary(
            item => item.CustomerId,
            item => item.Balance);
        var customerIdsWithDue = balances.Select(item => item.CustomerId).ToArray();
        var query = dbContext.Customers
            .AsNoTracking()
            .Where(customer => customerIdsWithDue.Contains(customer.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(customer =>
                EF.Functions.ILike(customer.Name, $"%{term}%") ||
                EF.Functions.ILike(customer.CustomerCode, $"%{term}%") ||
                EF.Functions.ILike(customer.Phone, $"%{term}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var customers = await query
            .OrderBy(customer => customer.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new
            {
                customer.Id,
                customer.CustomerCode,
                customer.Name,
                customer.Phone,
                customer.CreditLimit
            })
            .ToArrayAsync(cancellationToken);
        var customerIds = customers.Select(item => item.Id).ToArray();
        var invoiceDues = await dbContext.Sales
            .AsNoTracking()
            .Where(sale =>
                sale.CustomerId.HasValue &&
                customerIds.Contains(sale.CustomerId.Value) &&
                sale.Status != SaleStatus.Cancelled &&
                sale.GrandTotal - sale.ReturnedAmount >
                    sale.PaidAmount - sale.RefundedAmount)
            .GroupBy(sale => sale.CustomerId!.Value)
            .Select(group => new
            {
                CustomerId = group.Key,
                InvoiceDue = group.Sum(sale =>
                    sale.GrandTotal - sale.ReturnedAmount -
                    (sale.PaidAmount - sale.RefundedAmount)),
                Count = group.Count(),
                Oldest = group.Min(sale => sale.SaleDate)
            })
            .ToDictionaryAsync(item => item.CustomerId, cancellationToken);
        var items = customers.Select(customer =>
        {
            invoiceDues.TryGetValue(customer.Id, out var invoices);
            return new CustomerDueSummaryItem(
                customer.Id,
                customer.CustomerCode,
                customer.Name,
                customer.Phone,
                customer.CreditLimit,
                balanceByCustomer[customer.Id],
                invoices?.InvoiceDue ?? 0,
                invoices?.Count ?? 0,
                invoices?.Oldest);
        }).ToArray();

        return new PagedResult<CustomerDueSummaryItem>(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<CustomerAgingResult> GetAgingAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var invoices = await dbContext.Sales
            .AsNoTracking()
            .Where(sale =>
                sale.CustomerId.HasValue &&
                sale.Status != SaleStatus.Cancelled &&
                sale.GrandTotal - sale.ReturnedAmount >
                    sale.PaidAmount - sale.RefundedAmount)
            .Select(sale => new
            {
                CustomerId = sale.CustomerId!.Value,
                sale.Customer!.CustomerCode,
                CustomerName = sale.Customer.Name,
                sale.SaleDate,
                Due = sale.GrandTotal - sale.ReturnedAmount -
                    (sale.PaidAmount - sale.RefundedAmount)
            })
            .ToArrayAsync(cancellationToken);

        var customerItems = invoices
            .GroupBy(item => new
            {
                item.CustomerId,
                item.CustomerCode,
                item.CustomerName
            })
            .Select(group =>
            {
                var current = group
                    .Where(item => AgeDays(item.SaleDate, today) <= 30)
                    .Sum(item => item.Due);
                var days31To60 = group
                    .Where(item =>
                        AgeDays(item.SaleDate, today) is >= 31 and <= 60)
                    .Sum(item => item.Due);
                var days61To90 = group
                    .Where(item =>
                        AgeDays(item.SaleDate, today) is >= 61 and <= 90)
                    .Sum(item => item.Due);
                var over90 = group
                    .Where(item => AgeDays(item.SaleDate, today) > 90)
                    .Sum(item => item.Due);
                return new CustomerAgingItem(
                    group.Key.CustomerId,
                    group.Key.CustomerCode,
                    group.Key.CustomerName,
                    current,
                    days31To60,
                    days61To90,
                    over90,
                    current + days31To60 + days61To90 + over90);
            })
            .OrderByDescending(item => item.Total)
            .ToArray();

        return new CustomerAgingResult(
            customerItems.Sum(item => item.Current),
            customerItems.Sum(item => item.Days31To60),
            customerItems.Sum(item => item.Days61To90),
            customerItems.Sum(item => item.Over90),
            customerItems.Sum(item => item.Total),
            customerItems);
    }

    public async Task<PagedResult<CustomerReceiptListItem>> GetReceiptsAsync(
        string? search,
        Guid? customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.CustomerReceipts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(receipt =>
                EF.Functions.ILike(receipt.ReceiptNumber, $"%{term}%") ||
                EF.Functions.ILike(receipt.Customer.Name, $"%{term}%") ||
                (receipt.ReferenceNumber != null &&
                 EF.Functions.ILike(receipt.ReferenceNumber, $"%{term}%")));
        }
        if (customerId.HasValue)
        {
            query = query.Where(receipt => receipt.CustomerId == customerId);
        }
        if (from.HasValue)
        {
            query = query.Where(receipt => receipt.ReceivedOn >= from);
        }
        if (to.HasValue)
        {
            query = query.Where(receipt => receipt.ReceivedOn <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(receipt => receipt.ReceivedOn)
            .ThenByDescending(receipt => receipt.ReceiptNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(receipt => new CustomerReceiptListItem(
                receipt.Id,
                receipt.ReceiptNumber,
                receipt.CustomerId,
                receipt.Customer.CustomerCode,
                receipt.Customer.Name,
                receipt.PaymentMethod.Name,
                receipt.Amount,
                receipt.AllocatedAmount,
                receipt.Amount - receipt.AllocatedAmount,
                receipt.ReceivedOn,
                receipt.ReferenceNumber))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<CustomerReceiptListItem>(
            items,
            page,
            pageSize,
            totalCount);
    }

    public async Task<OperationResult<CustomerReceiptDetailItem>> GetReceiptAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectReceipt(
                dbContext.CustomerReceipts
                    .AsNoTracking()
                    .Where(receipt => receipt.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<CustomerReceiptDetailItem>.Failure(
                "Customer receipt not found.")
            : OperationResult<CustomerReceiptDetailItem>.Success(item);
    }

    public async Task<OperationResult<CustomerReceiptDetailItem>> CreateReceiptAsync(
        CreateCustomerReceiptRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return OperationResult<CustomerReceiptDetailItem>.Failure(
                "Receipt amount must be greater than zero.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var customer = await dbContext.Customers.SingleOrDefaultAsync(
            item => item.Id == request.CustomerId && item.IsActive,
            cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerReceiptDetailItem>.Failure(
                "Customer is missing or inactive.");
        }
        if (!await dbContext.PaymentMethods.AnyAsync(
                method => method.Id == request.PaymentMethodId && method.IsActive,
                cancellationToken))
        {
            return OperationResult<CustomerReceiptDetailItem>.Failure(
                "Payment method is missing or inactive.");
        }

        var accountBalance = await dbContext.CustomerLedgerEntries
            .Where(entry => entry.CustomerId == customer.Id)
            .SumAsync(
                entry => (decimal?)(entry.Debit - entry.Credit),
                cancellationToken) ?? 0;
        if (accountBalance <= 0)
        {
            return OperationResult<CustomerReceiptDetailItem>.Failure(
                "This customer does not have an outstanding balance.");
        }
        if (request.Amount > accountBalance)
        {
            return OperationResult<CustomerReceiptDetailItem>.Failure(
                "Receipt amount cannot exceed the customer balance.");
        }

        var receipt = new CustomerReceipt(
            GenerateNumber("RCP"),
            customer.Id,
            request.PaymentMethodId,
            request.Amount,
            request.ReceivedOn,
            performedBy,
            request.ReferenceNumber,
            request.Notes);
        var remaining = request.Amount;
        var sales = await dbContext.Sales
            .Where(sale =>
                sale.CustomerId == customer.Id &&
                sale.Status != SaleStatus.Cancelled &&
                sale.GrandTotal - sale.ReturnedAmount >
                    sale.PaidAmount - sale.RefundedAmount)
            .OrderBy(sale => sale.SaleDate)
            .ThenBy(sale => sale.InvoiceNumber)
            .ToArrayAsync(cancellationToken);
        foreach (var sale in sales)
        {
            if (remaining <= 0)
            {
                break;
            }
            var amount = Math.Min(remaining, sale.DueAmount);
            var payment = new SalePayment(
                sale.Id,
                request.PaymentMethodId,
                amount,
                request.ReceivedOn,
                performedBy,
                receipt.ReceiptNumber,
                request.Notes);
            sale.AddPayment(payment);
            dbContext.SalePayments.Add(payment);
            receipt.Allocate(sale.Id, amount);
            remaining -= amount;
        }

        dbContext.CustomerReceipts.Add(receipt);
        dbContext.CustomerLedgerEntries.Add(new CustomerLedgerEntry(
            customer.Id,
            receipt.ReceivedOn,
            CustomerLedgerEntryType.DueCollection,
            0,
            receipt.Amount,
            "CustomerReceipt",
            receipt.Id,
            receipt.ReceiptNumber,
            performedBy,
            receipt.Notes));

        var error = await SaveTransactionAsync(transaction, cancellationToken);
        if (error is not null)
        {
            return OperationResult<CustomerReceiptDetailItem>.Failure(error);
        }

        await auditService.WriteAsync(
            "CollectDue",
            "CustomerReceipt",
            receipt.Id.ToString(),
            $"Recorded customer receipt {receipt.ReceiptNumber}.",
            performedBy,
            cancellationToken);
        return await GetReceiptAsync(receipt.Id, cancellationToken);
    }

    public async Task<OperationResult<CustomerStatementResult>> GetStatementAsync(
        Guid customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(item => item.Id == customerId)
            .Select(item => new
            {
                item.Id,
                item.CustomerCode,
                item.Name,
                item.Phone,
                item.Address
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerStatementResult>.Failure(
                "Customer not found.");
        }

        var allEntries = await dbContext.CustomerLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.CustomerId == customerId)
            .OrderBy(entry => entry.EntryDate)
            .ThenBy(entry => entry.CreatedOn)
            .ThenBy(entry => entry.Id)
            .ToArrayAsync(cancellationToken);
        var openingBalance = allEntries
            .Where(entry => from.HasValue && entry.EntryDate < from)
            .Sum(entry => entry.Debit - entry.Credit);
        var balance = openingBalance;
        var entries = allEntries
            .Where(entry => !from.HasValue || entry.EntryDate >= from)
            .Where(entry => !to.HasValue || entry.EntryDate <= to)
            .Select(entry =>
            {
                balance += entry.Debit - entry.Credit;
                return new CustomerStatementEntry(
                    entry.Id,
                    entry.EntryDate,
                    entry.EntryType,
                    entry.Debit,
                    entry.Credit,
                    balance,
                    entry.ReferenceType,
                    entry.ReferenceId,
                    entry.ReferenceNumber,
                    entry.Notes);
            })
            .ToArray();

        return OperationResult<CustomerStatementResult>.Success(
            new CustomerStatementResult(
                customer.Id,
                customer.CustomerCode,
                customer.Name,
                customer.Phone,
                customer.Address,
                from,
                to,
                openingBalance,
                balance,
                entries));
    }

    public async Task<OperationResult<CustomerStatementResult>> CreateAdjustmentAsync(
        CreateCustomerAdjustmentRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reason))
        {
            return OperationResult<CustomerStatementResult>.Failure(
                "Adjustment amount and reason are required.");
        }
        if (!await dbContext.Customers.AnyAsync(
                customer => customer.Id == request.CustomerId,
                cancellationToken))
        {
            return OperationResult<CustomerStatementResult>.Failure(
                "Customer not found.");
        }

        var adjustmentId = Guid.CreateVersion7();
        var referenceNumber = GenerateNumber("ADJ");
        dbContext.CustomerLedgerEntries.Add(new CustomerLedgerEntry(
            request.CustomerId,
            request.AdjustmentDate,
            CustomerLedgerEntryType.ManualAdjustment,
            request.Direction == CustomerAdjustmentDirection.Debit
                ? request.Amount
                : 0,
            request.Direction == CustomerAdjustmentDirection.Credit
                ? request.Amount
                : 0,
            "CustomerAdjustment",
            adjustmentId,
            referenceNumber,
            performedBy,
            request.Reason));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "Adjust",
            "CustomerLedger",
            adjustmentId.ToString(),
            $"{request.Direction} adjustment {referenceNumber}: {request.Reason.Trim()}",
            performedBy,
            cancellationToken);
        return await GetStatementAsync(
            request.CustomerId,
            null,
            null,
            cancellationToken);
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
            return "Customer balance changed concurrently. Reload and try again.";
        }
        catch (DbUpdateException)
        {
            return "The receipt could not be saved because a conflicting record exists.";
        }
    }

    private static int AgeDays(DateTimeOffset date, DateTime today) =>
        Math.Max(0, (today - date.UtcDateTime.Date).Days);

    private static string GenerateNumber(string prefix) =>
        $"{prefix}-{DateTimeOffset.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..25]
            .ToUpperInvariant();

    private static IQueryable<CustomerReceiptDetailItem> ProjectReceipt(
        IQueryable<CustomerReceipt> query) =>
        query.Select(receipt => new CustomerReceiptDetailItem(
            receipt.Id,
            receipt.ReceiptNumber,
            receipt.CustomerId,
            receipt.Customer.CustomerCode,
            receipt.Customer.Name,
            receipt.Customer.Phone,
            receipt.Customer.Address,
            receipt.PaymentMethodId,
            receipt.PaymentMethod.Name,
            receipt.Amount,
            receipt.AllocatedAmount,
            receipt.Amount - receipt.AllocatedAmount,
            receipt.ReceivedOn,
            receipt.ReferenceNumber,
            receipt.Notes,
            receipt.Allocations
                .OrderBy(allocation => allocation.Sale.SaleDate)
                .Select(allocation => new CustomerReceiptAllocationItem(
                    allocation.Id,
                    allocation.SaleId,
                    allocation.Sale.InvoiceNumber,
                    allocation.Sale.SaleDate,
                    allocation.Amount))
                .ToArray()));
}
