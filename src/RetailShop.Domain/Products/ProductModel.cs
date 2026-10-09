using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class ProductModel : AuditableEntity
{
    private ProductModel()
    {
    }

    public ProductModel(Guid brandId, string name)
    {
        BrandId = brandId;
        Rename(name);
    }

    public Guid BrandId { get; private set; }

    public Brand Brand { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public void Update(Guid brandId, string name)
    {
        BrandId = brandId;
        Rename(name);
    }

    private void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }
}
