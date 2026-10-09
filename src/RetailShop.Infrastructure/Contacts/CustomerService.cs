using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Contacts;
using RetailShop.Domain.Contacts;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Contacts;

internal sealed class CustomerService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : ICustomerService
{
    public async Task<PagedResult<CustomerListItem>> GetAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(customer =>
                EF.Functions.ILike(customer.Name, $"%{term}%") ||
                EF.Functions.ILike(customer.CustomerCode, $"%{term}%") ||
                EF.Functions.ILike(customer.Phone, $"%{term}%") ||
                (customer.Email != null && EF.Functions.ILike(customer.Email, $"%{term}%")));
        }

        if (isActive.HasValue)
        {
            query = query.Where(customer => customer.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(customer => customer.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new CustomerListItem(
                customer.Id,
                customer.CustomerCode,
                customer.Name,
                customer.Phone,
                customer.Email,
                customer.Address,
                customer.Notes,
                customer.CreditLimit,
                customer.IsActive))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<CustomerListItem>(items, page, pageSize, totalCount);
    }

    public async Task<OperationResult<CustomerListItem>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(customer => new CustomerListItem(
                customer.Id,
                customer.CustomerCode,
                customer.Name,
                customer.Phone,
                customer.Email,
                customer.Address,
                customer.Notes,
                customer.CreditLimit,
                customer.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<CustomerListItem>.Failure("Customer not found.")
            : OperationResult<CustomerListItem>.Success(item);
    }

    public async Task<OperationResult<CustomerListItem>> CreateAsync(
        SaveCustomerRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return OperationResult<CustomerListItem>.Failure(errors);
        }

        var customer = new Customer(
            await GenerateCodeAsync(cancellationToken),
            request.Name,
            request.Phone)
        {
            CreatedBy = performedBy
        };
        Apply(customer, request);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("Create", customer, performedBy, cancellationToken);
        return OperationResult<CustomerListItem>.Success(Map(customer));
    }

    public async Task<OperationResult<CustomerListItem>> UpdateAsync(
        Guid id,
        SaveCustomerRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FindAsync([id], cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerListItem>.Failure("Customer not found.");
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return OperationResult<CustomerListItem>.Failure(errors);
        }

        Apply(customer, request);
        customer.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("Update", customer, performedBy, cancellationToken);
        return OperationResult<CustomerListItem>.Success(Map(customer));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.FindAsync([id], cancellationToken);
        if (customer is null)
        {
            return OperationResult<bool>.Failure("Customer not found.");
        }

        if (await dbContext.CustomerLedgerEntries.AnyAsync(
                entry => entry.CustomerId == id,
                cancellationToken))
        {
            return OperationResult<bool>.Failure(
                "This customer has financial history and cannot be deleted. Mark it inactive instead.");
        }

        customer.DeletedBy = performedBy;
        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("Delete", customer, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private async Task<string> GenerateCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"CUS-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            if (!await dbContext.Customers.IgnoreQueryFilters().AnyAsync(
                    customer => customer.CustomerCode == code,
                    cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique customer code.");
    }

    private Task AuditAsync(
        string action,
        Customer customer,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        auditService.WriteAsync(
            action,
            "Customer",
            customer.Id.ToString(),
            $"{action} customer '{customer.Name}' ({customer.CustomerCode}).",
            performedBy,
            cancellationToken);

    private static List<string> Validate(SaveCustomerRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Customer name is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            errors.Add("Customer phone is required.");
        }
        if (request.CreditLimit < 0)
        {
            errors.Add("Credit limit cannot be negative.");
        }
        return errors;
    }

    private static void Apply(Customer customer, SaveCustomerRequest request)
    {
        customer.Name = request.Name.Trim();
        customer.Phone = request.Phone.Trim();
        customer.Email = Clean(request.Email);
        customer.Address = Clean(request.Address);
        customer.Notes = Clean(request.Notes);
        customer.CreditLimit = request.CreditLimit;
        customer.IsActive = request.IsActive;
    }

    private static CustomerListItem Map(Customer customer) =>
        new(
            customer.Id,
            customer.CustomerCode,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.Notes,
            customer.CreditLimit,
            customer.IsActive);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
