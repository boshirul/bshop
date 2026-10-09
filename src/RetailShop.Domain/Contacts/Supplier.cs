using RetailShop.Domain.Common;

namespace RetailShop.Domain.Contacts;

public sealed class Supplier : AuditableEntity
{
    private Supplier()
    {
    }

    public Supplier(string supplierCode, string name, string phone)
    {
        SupplierCode = supplierCode.Trim();
        Name = name.Trim();
        Phone = phone.Trim();
    }

    public string SupplierCode { get; private set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
