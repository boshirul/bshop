using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class ProductBarcode : AuditableEntity
{
    private ProductBarcode()
    {
    }

    public ProductBarcode(Guid productId, string value, bool isPrimary)
    {
        ProductId = productId;
        Value = value.Trim();
        IsPrimary = isPrimary;
    }

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public string Value { get; private set; } = string.Empty;

    public bool IsPrimary { get; set; }
}
