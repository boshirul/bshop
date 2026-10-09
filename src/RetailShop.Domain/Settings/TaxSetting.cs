using RetailShop.Domain.Common;

namespace RetailShop.Domain.Settings;

public sealed class TaxSetting : AuditableEntity
{
    public string TaxName { get; set; } = "VAT";

    public decimal DefaultRate { get; set; }

    public bool IsEnabled { get; set; }
}
