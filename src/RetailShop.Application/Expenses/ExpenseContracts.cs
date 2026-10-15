using System.ComponentModel.DataAnnotations;
using RetailShop.Application.Common;

namespace RetailShop.Application.Expenses;

public sealed record ExpenseCategoryItem(Guid Id, string Code, string Name, bool IsActive);
public sealed record SaveExpenseCategoryRequest([param: Required, MaxLength(30)] string Code, [param: Required, MaxLength(150)] string Name, bool IsActive = true);
public sealed record ExpenseLineRequest(Guid CategoryId, [param: Required, MaxLength(500)] string Description, [param: Range(typeof(decimal), "0.01", "999999999")] decimal Amount, string? Reference);
public sealed record SaveExpenseVoucherRequest(DateOnly ExpenseDate, [param: Required, MaxLength(200)] string Payee, Guid? PaymentMethodId, string? GeneralRemarks, IReadOnlyCollection<ExpenseLineRequest> Lines);
public sealed record ExpenseVoucherLineItem(Guid Id, Guid CategoryId, string CategoryName, string Description, decimal Amount, string? Reference);
public sealed record ExpenseVoucherItem(Guid Id, string VoucherNumber, DateOnly ExpenseDate, string Payee, Guid? PaymentMethodId, string? PaymentMethodName, string? GeneralRemarks, string Status, decimal TotalAmount, IReadOnlyCollection<ExpenseVoucherLineItem> Lines);
public sealed record ExpenseVoucherListItem(Guid Id, string VoucherNumber, DateOnly ExpenseDate, string Payee, string? PaymentMethodName, string Status, decimal TotalAmount);
public interface IExpenseService
{
    Task<IReadOnlyCollection<ExpenseCategoryItem>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<OperationResult<ExpenseCategoryItem>> SaveCategoryAsync(Guid? id, SaveExpenseCategoryRequest request, Guid userId, CancellationToken cancellationToken);
    Task<PagedResult<ExpenseVoucherListItem>> GetVouchersAsync(string? search, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken);
    Task<OperationResult<ExpenseVoucherItem>> GetVoucherAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<ExpenseVoucherItem>> CreateAsync(SaveExpenseVoucherRequest request, Guid userId, CancellationToken cancellationToken);
    Task<OperationResult<ExpenseVoucherItem>> UpdateAsync(Guid id, SaveExpenseVoucherRequest request, Guid userId, CancellationToken cancellationToken);
    Task<OperationResult<ExpenseVoucherItem>> PostAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<OperationResult<ExpenseVoucherItem>> VoidAsync(Guid id, string reason, Guid userId, CancellationToken cancellationToken);
}
