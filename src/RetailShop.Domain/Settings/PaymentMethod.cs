using RetailShop.Domain.Common;

namespace RetailShop.Domain.Settings;

public sealed class PaymentMethod : AuditableEntity
{
    private PaymentMethod()
    {
    }

    public PaymentMethod(string name, string code, string type)
    {
        Apply(name, code, type);
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string Type { get; private set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public void Update(string name, string code, string type) => Apply(name, code, type);

    private void Apply(string name, string code, string type)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Code = code.Trim().ToUpperInvariant();
        Type = type.Trim();
    }
}
