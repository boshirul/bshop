using Microsoft.EntityFrameworkCore;
using RetailShop.Application.Auditing;
using RetailShop.Application.Common;
using RetailShop.Application.Contacts;
using RetailShop.Domain.Contacts;
using RetailShop.Infrastructure.Persistence;

namespace RetailShop.Infrastructure.Contacts;

internal sealed class SupplierService(
    RetailShopDbContext dbContext,
    IAuditService auditService) : ISupplierService
{
    public async Task<PagedResult<SupplierListItem>> GetAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(supplier =>
                EF.Functions.ILike(supplier.Name, $"%{term}%") ||
                EF.Functions.ILike(supplier.SupplierCode, $"%{term}%") ||
                EF.Functions.ILike(supplier.Phone, $"%{term}%") ||
                (supplier.ContactPerson != null &&
                 EF.Functions.ILike(supplier.ContactPerson, $"%{term}%")));
        }

        if (isActive.HasValue)
        {
            query = query.Where(supplier => supplier.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(supplier => supplier.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(supplier => new SupplierListItem(
                supplier.Id,
                supplier.SupplierCode,
                supplier.Name,
                supplier.ContactPerson,
                supplier.Phone,
                supplier.Email,
                supplier.Address,
                supplier.Notes,
                supplier.IsActive))
            .ToArrayAsync(cancellationToken);

        return new PagedResult<SupplierListItem>(items, page, pageSize, totalCount);
    }

    public async Task<OperationResult<SupplierListItem>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.Id == id)
            .Select(supplier => new SupplierListItem(
                supplier.Id,
                supplier.SupplierCode,
                supplier.Name,
                supplier.ContactPerson,
                supplier.Phone,
                supplier.Email,
                supplier.Address,
                supplier.Notes,
                supplier.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? OperationResult<SupplierListItem>.Failure("Supplier not found.")
            : OperationResult<SupplierListItem>.Success(item);
    }

    public async Task<OperationResult<SupplierListItem>> CreateAsync(
        SaveSupplierRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return OperationResult<SupplierListItem>.Failure(errors);
        }

        var supplier = new Supplier(
            await GenerateCodeAsync(cancellationToken),
            request.Name,
            request.Phone)
        {
            CreatedBy = performedBy
        };
        Apply(supplier, request);
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("Create", supplier, performedBy, cancellationToken);
        return OperationResult<SupplierListItem>.Success(Map(supplier));
    }

    public async Task<OperationResult<SupplierListItem>> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FindAsync([id], cancellationToken);
        if (supplier is null)
        {
            return OperationResult<SupplierListItem>.Failure("Supplier not found.");
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return OperationResult<SupplierListItem>.Failure(errors);
        }

        Apply(supplier, request);
        supplier.LastModifiedBy = performedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("Update", supplier, performedBy, cancellationToken);
        return OperationResult<SupplierListItem>.Success(Map(supplier));
    }

    public async Task<OperationResult<bool>> DeleteAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FindAsync([id], cancellationToken);
        if (supplier is null)
        {
            return OperationResult<bool>.Failure("Supplier not found.");
        }

        if (await dbContext.SupplierLedgerEntries.AnyAsync(
                entry => entry.SupplierId == id,
                cancellationToken))
        {
            return OperationResult<bool>.Failure(
                "This supplier has financial history and cannot be deleted. Mark it inactive instead.");
        }

        supplier.DeletedBy = performedBy;
        dbContext.Suppliers.Remove(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("Delete", supplier, performedBy, cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private async Task<string> GenerateCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"SUP-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
            if (!await dbContext.Suppliers.IgnoreQueryFilters().AnyAsync(
                    supplier => supplier.SupplierCode == code,
                    cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique supplier code.");
    }

    private Task AuditAsync(
        string action,
        Supplier supplier,
        Guid performedBy,
        CancellationToken cancellationToken) =>
        auditService.WriteAsync(
            action,
            "Supplier",
            supplier.Id.ToString(),
            $"{action} supplier '{supplier.Name}' ({supplier.SupplierCode}).",
            performedBy,
            cancellationToken);

    private static List<string> Validate(SaveSupplierRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Supplier name is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            errors.Add("Supplier phone is required.");
        }
        return errors;
    }

    private static void Apply(Supplier supplier, SaveSupplierRequest request)
    {
        supplier.Name = request.Name.Trim();
        supplier.ContactPerson = Clean(request.ContactPerson);
        supplier.Phone = request.Phone.Trim();
        supplier.Email = Clean(request.Email);
        supplier.Address = Clean(request.Address);
        supplier.Notes = Clean(request.Notes);
        supplier.IsActive = request.IsActive;
    }

    private static SupplierListItem Map(Supplier supplier) =>
        new(
            supplier.Id,
            supplier.SupplierCode,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.Notes,
            supplier.IsActive);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
