using RetailShop.Domain.Common;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Products;
using RetailShop.Domain.Purchases;
using RetailShop.Domain.Sales;

namespace RetailShop.Domain.Warranty;

public sealed class ProductSerial : AuditableEntity
{
    private ProductSerial()
    {
    }

    public ProductSerial(
        Guid productId,
        string serialNumber,
        Guid? purchaseId,
        Guid? purchaseDetailId,
        Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new ArgumentException(
                "Serial number is required.",
                nameof(serialNumber));
        }

        ProductId = productId;
        SerialNumber = serialNumber.Trim();
        NormalizedSerialNumber = Normalize(serialNumber);
        PurchaseId = purchaseId;
        PurchaseDetailId = purchaseDetailId;
        Status = ProductSerialStatus.Available;
        CreatedBy = createdBy;
    }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public string SerialNumber { get; private set; } = string.Empty;
    public string NormalizedSerialNumber { get; private set; } = string.Empty;
    public ProductSerialStatus Status { get; private set; }
    public Guid? PurchaseId { get; private set; }
    public Purchase? Purchase { get; private set; }
    public Guid? PurchaseDetailId { get; private set; }
    public PurchaseDetail? PurchaseDetail { get; private set; }
    public Guid? SaleId { get; private set; }
    public Sale? Sale { get; private set; }
    public Guid? SaleDetailId { get; private set; }
    public SaleDetail? SaleDetail { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public DateTimeOffset? SoldOn { get; private set; }
    public DateOnly? WarrantyStartDate { get; private set; }
    public DateOnly? WarrantyExpiryDate { get; private set; }

    public bool IsWarrantyActive(DateOnly asOf) =>
        WarrantyStartDate.HasValue &&
        WarrantyExpiryDate.HasValue &&
        WarrantyStartDate.Value <= asOf &&
        WarrantyExpiryDate.Value >= asOf;

    public void MarkSold(
        Guid saleId,
        Guid saleDetailId,
        Guid? customerId,
        DateTimeOffset saleDate,
        int? warrantyMonths,
        Guid performedBy)
    {
        if (Status != ProductSerialStatus.Available)
        {
            throw new InvalidOperationException(
                $"Serial '{SerialNumber}' is not available for sale.");
        }
        if (!warrantyMonths.HasValue || warrantyMonths <= 0)
        {
            throw new InvalidOperationException(
                $"Product serial '{SerialNumber}' does not have a valid warranty period.");
        }

        SaleId = saleId;
        SaleDetailId = saleDetailId;
        CustomerId = customerId;
        SoldOn = saleDate;
        WarrantyStartDate = DateOnly.FromDateTime(saleDate.UtcDateTime);
        WarrantyExpiryDate = WarrantyStartDate.Value.AddMonths(warrantyMonths.Value);
        Status = ProductSerialStatus.Sold;
        LastModifiedBy = performedBy;
    }

    public void MoveToWarranty(Guid performedBy)
    {
        EnsureSoldForClaim();
        Status = ProductSerialStatus.InWarranty;
        LastModifiedBy = performedBy;
    }

    public void MoveToSupplierClaim(Guid performedBy)
    {
        EnsureSoldForClaim();
        Status = ProductSerialStatus.SupplierClaim;
        LastModifiedBy = performedBy;
    }

    public void MarkRepaired(Guid performedBy)
    {
        Status = ProductSerialStatus.Repaired;
        LastModifiedBy = performedBy;
    }

    public void MarkReplaced(Guid performedBy)
    {
        Status = ProductSerialStatus.Replaced;
        LastModifiedBy = performedBy;
    }

    public void MarkRetired(Guid performedBy)
    {
        Status = ProductSerialStatus.Retired;
        LastModifiedBy = performedBy;
    }

    private void EnsureSoldForClaim()
    {
        if (SaleId is null)
        {
            throw new InvalidOperationException(
                $"Serial '{SerialNumber}' has not been sold.");
        }
    }

    public static string Normalize(string serialNumber) =>
        serialNumber.Trim().ToUpperInvariant();
}
