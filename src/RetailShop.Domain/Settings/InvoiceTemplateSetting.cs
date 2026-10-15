using RetailShop.Domain.Common;

namespace RetailShop.Domain.Settings;

public enum InvoicePrintFormat { A4, Short }
public enum InvoiceLogoPlacement { Left, Center }

/// <summary>Installation-wide presentation options. It deliberately contains no report fields or formulas.</summary>
public sealed class InvoiceTemplateSetting : AuditableEntity
{
    public InvoicePrintFormat DefaultFormat { get; set; } = InvoicePrintFormat.A4;
    public bool A4Enabled { get; set; } = true;
    public bool ShortEnabled { get; set; } = true;
    public bool ShowLogo { get; set; } = true;
    public InvoiceLogoPlacement LogoPlacement { get; set; } = InvoiceLogoPlacement.Left;
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public string AccentColor { get; set; } = "#087CFF";
    public int ThermalMarginMm { get; set; } = 3;
    public int Version { get; set; } = 1;
}
