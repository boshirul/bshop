using RetailShop.Domain.Common;

namespace RetailShop.Domain.Expenses;

public sealed class ExpenseCategory : AuditableEntity
{
    private ExpenseCategory() { }
    public ExpenseCategory(string code, string name)
    {
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public ICollection<ExpenseVoucherDetail> Details { get; private set; } = [];
    public void Update(string code, string name, bool isActive)
    {
        Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); IsActive = isActive;
    }
}
