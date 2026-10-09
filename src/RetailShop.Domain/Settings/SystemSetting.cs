using RetailShop.Domain.Common;

namespace RetailShop.Domain.Settings;

public sealed class SystemSetting : AuditableEntity
{
    public string CurrencyCode { get; set; } = "BDT";

    public string TimeZone { get; set; } = "Asia/Dhaka";

    public string DateFormat { get; set; } = "dd MMM yyyy";

    public int DefaultPageSize { get; set; } = 25;

    public bool LowStockAlertsEnabled { get; set; } = true;
}
