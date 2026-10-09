using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class ProductImage : AuditableEntity
{
    private ProductImage()
    {
    }

    public ProductImage(Guid productId, string url, string? altText, bool isPrimary)
    {
        ProductId = productId;
        Url = url.Trim();
        AltText = altText?.Trim();
        IsPrimary = isPrimary;
    }

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public string Url { get; private set; } = string.Empty;

    public string? AltText { get; private set; }

    public bool IsPrimary { get; set; }
}
