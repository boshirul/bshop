using RetailShop.Domain.Common;

namespace RetailShop.Domain.Products;

public sealed class SubCategory : AuditableEntity
{
    private SubCategory()
    {
    }

    public SubCategory(Guid categoryId, string name)
    {
        CategoryId = categoryId;
        Rename(name);
    }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public void Update(Guid categoryId, string name)
    {
        CategoryId = categoryId;
        Rename(name);
    }

    private void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }
}
