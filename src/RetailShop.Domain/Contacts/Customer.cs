using RetailShop.Domain.Common;

namespace RetailShop.Domain.Contacts;

public sealed class Customer : AuditableEntity
{
    private Customer()
    {
    }

    public Customer(string customerCode, string name, string phone)
    {
        CustomerCode = customerCode.Trim();
        Name = name.Trim();
        Phone = phone.Trim();
    }

    public string CustomerCode { get; private set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Notes { get; set; }

    public decimal CreditLimit { get; set; }

    public bool IsActive { get; set; } = true;
}
