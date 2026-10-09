using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class Product : AuditableEntity
{
    private Product()
    {
    }

    public Product(
        string productCode,
        string name,
        Guid categoryId,
        Guid unitId)
    {
        ProductCode = productCode;
        Name = name.Trim();
        CategoryId = categoryId;
        UnitId = unitId;
    }

    public string ProductCode { get; private set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public Guid? SubCategoryId { get; set; }

    public SubCategory? SubCategory { get; set; }

    public Guid? BrandId { get; set; }

    public Brand? Brand { get; set; }

    public Guid? ProductModelId { get; set; }

    public ProductModel? ProductModel { get; set; }

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!;

    public string? VariantName { get; set; }

    public string? Description { get; set; }

    public decimal PurchasePrice { get; set; }

    public decimal SalePrice { get; set; }

    public decimal AverageCost { get; set; }

    public decimal MinimumStockLevel { get; set; }

    public bool IsWarrantyAvailable { get; set; }

    public int? WarrantyMonths { get; set; }

    public bool IsSerialRequired { get; set; }

    public bool IsVatApplicable { get; set; }

    public bool AllowOnlineSale { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductImage> Images { get; set; } = [];

    public ICollection<ProductBarcode> Barcodes { get; set; } = [];
}
