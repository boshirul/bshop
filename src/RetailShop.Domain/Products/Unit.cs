using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class Unit : AuditableEntity
{
    private Unit()
    {
    }

    public Unit(string name, string symbol)
    {
        Update(name, symbol);
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string Symbol { get; private set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public void Update(string name, string symbol)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Symbol = symbol.Trim();
    }
}
