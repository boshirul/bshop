using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class Brand : AuditableEntity
{
    private Brand()
    {
    }

    public Brand(string name)
    {
        Rename(name);
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ProductModel> Models { get; set; } = [];

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }
}
