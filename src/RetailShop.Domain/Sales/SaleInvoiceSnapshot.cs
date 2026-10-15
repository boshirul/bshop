using RetailShop.Domain.Common;

namespace RetailShop.Domain.Sales;

/// <summary>Immutable issue-time presentation payload; financial source of truth remains Sale and its details.</summary>
public sealed class SaleInvoiceSnapshot : AuditableEntity
{
    private SaleInvoiceSnapshot() { }
    public SaleInvoiceSnapshot(Guid saleId, int templateVersion, string content)
    {
        SaleId = saleId; TemplateVersion = templateVersion; Content = content;
    }
    public Guid SaleId { get; private set; }
    public Sale Sale { get; private set; } = null!;
    public int TemplateVersion { get; private set; }
    public string Content { get; private set; } = string.Empty;
}
