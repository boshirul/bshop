using RetailShop.Domain.Common;

namespace RetailShop.Domain.Settings;

public sealed class InvoiceSetting : AuditableEntity
{
    public string InvoicePrefix { get; set; } = "INV";

    public int NextInvoiceNumber { get; set; } = 1;

    public string? TermsAndConditions { get; set; }

    public string? ReturnPolicy { get; set; }

    public bool ShowTaxDetails { get; set; } = true;

    public bool ShowQrCode { get; set; } = true;
}
