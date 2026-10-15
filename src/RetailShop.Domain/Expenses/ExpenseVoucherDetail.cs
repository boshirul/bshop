using RetailShop.Domain.Common;

namespace RetailShop.Domain.Expenses;

public sealed class ExpenseVoucherDetail : AuditableEntity
{
    private ExpenseVoucherDetail() { }
    public ExpenseVoucherDetail(Guid categoryId, string description, decimal amount, string? reference)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ExpenseCategoryId = categoryId; Description = description.Trim(); Amount = amount; Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
    }
    public Guid ExpenseVoucherId { get; private set; }
    public ExpenseVoucher Voucher { get; private set; } = null!;
    public Guid ExpenseCategoryId { get; private set; }
    public ExpenseCategory Category { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
}
