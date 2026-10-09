using RetailShop.Domain.Common;

namespace RetailShop.Domain.Settings;

public sealed class ShopSetting : AuditableEntity
{
    public string ShopName { get; set; } = "KhanShop";

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? LogoUrl { get; set; }

    public string? TaxRegistrationNumber { get; set; }

    public string ReceiptFooter { get; set; } = "Thank you for shopping with us.";
}
