using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;

namespace RetailShop.Application.Contacts;

public sealed record CustomerListItem(
    Guid Id,
    string CustomerCode,
    string Name,
    string Phone,
    string? Email,
    string? Address,
    string? Notes,
    decimal CreditLimit,
    bool IsActive);

public sealed record SaveCustomerRequest(
    [param: Required, MaxLength(200)] string Name,
    [param: Required, MaxLength(30)] string Phone,
    [param: EmailAddress, MaxLength(256)] string? Email,
    [param: MaxLength(500)] string? Address,
    [param: MaxLength(1000)] string? Notes,
    decimal CreditLimit,
    bool IsActive = true);

public sealed record SupplierListItem(
    Guid Id,
    string SupplierCode,
    string Name,
    string? ContactPerson,
    string Phone,
    string? Email,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record SaveSupplierRequest(
    [param: Required, MaxLength(200)] string Name,
    [param: MaxLength(200)] string? ContactPerson,
    [param: Required, MaxLength(30)] string Phone,
    [param: EmailAddress, MaxLength(256)] string? Email,
    [param: MaxLength(500)] string? Address,
    [param: MaxLength(1000)] string? Notes,
    bool IsActive = true);

public interface ICustomerService
{
    Task<PagedResult<CustomerListItem>> GetAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<OperationResult<CustomerListItem>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<OperationResult<CustomerListItem>> CreateAsync(
        SaveCustomerRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<CustomerListItem>> UpdateAsync(
        Guid id,
        SaveCustomerRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);
}

public interface ISupplierService
{
    Task<PagedResult<SupplierListItem>> GetAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<OperationResult<SupplierListItem>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
    Task<OperationResult<SupplierListItem>> CreateAsync(
        SaveSupplierRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<SupplierListItem>> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        Guid performedBy,
        CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteAsync(
        Guid id,
        Guid performedBy,
        CancellationToken cancellationToken);
}
