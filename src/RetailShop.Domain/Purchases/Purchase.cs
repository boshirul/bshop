using RetailShop.Domain.Common;
using RetailShop.Domain.Contacts;

namespace RetailShop.Domain.Purchases;

public sealed class Purchase : AuditableEntity
{
    private Purchase()
    {
    }

    public Purchase(
        string purchaseNumber,
        Guid supplierId,
        DateTimeOffset purchaseDate,
        string? supplierInvoiceNumber,
        string? notes)
    {
        PurchaseNumber = purchaseNumber.Trim();
        SupplierId = supplierId;
        PurchaseDate = purchaseDate;
        SupplierInvoiceNumber = Clean(supplierInvoiceNumber);
        Notes = Clean(notes);
    }

    public string PurchaseNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public string? SupplierInvoiceNumber { get; private set; }
    public DateTimeOffset PurchaseDate { get; private set; }
    public PurchaseStatus Status { get; private set; } = PurchaseStatus.Confirmed;
    public PurchasePaymentStatus PaymentStatus { get; private set; } =
        PurchasePaymentStatus.Unpaid;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal ReturnedAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal DueAmount => GrandTotal - ReturnedAmount - PaidAmount;
    public string? Notes { get; private set; }
    public ICollection<PurchaseDetail> Details { get; private set; } = [];
    public ICollection<PurchasePayment> Payments { get; private set; } = [];
    public ICollection<PurchaseReturn> Returns { get; private set; } = [];

    public void AddDetail(
        Guid productId,
        decimal quantity,
        decimal unitCost,
        decimal discountAmount,
        decimal vatAmount)
    {
        var detail = new PurchaseDetail(
            Id,
            productId,
            quantity,
            unitCost,
            discountAmount,
            vatAmount);
        Details.Add(detail);
        RecalculateTotals();
    }

    public void AddPayment(PurchasePayment payment)
    {
        if (payment.Amount > DueAmount)
        {
            throw new InvalidOperationException(
                "Payment cannot exceed the outstanding purchase amount.");
        }

        Payments.Add(payment);
        PaidAmount += payment.Amount;
        UpdatePaymentStatus();
    }

    public void RegisterReturn(decimal amount)
    {
        if (amount <= 0 || ReturnedAmount + amount > GrandTotal)
        {
            throw new InvalidOperationException("Invalid purchase return amount.");
        }

        ReturnedAmount += amount;
        Status = ReturnedAmount == GrandTotal
            ? PurchaseStatus.Returned
            : PurchaseStatus.PartiallyReturned;
        UpdatePaymentStatus();
    }

    private void RecalculateTotals()
    {
        Subtotal = Details.Sum(item => item.GrossAmount);
        DiscountAmount = Details.Sum(item => item.DiscountAmount);
        VatAmount = Details.Sum(item => item.VatAmount);
        GrandTotal = Details.Sum(item => item.LineTotal);
        UpdatePaymentStatus();
    }

    private void UpdatePaymentStatus()
    {
        PaymentStatus = DueAmount switch
        {
            < 0 => PurchasePaymentStatus.Credit,
            0 => PurchasePaymentStatus.Paid,
            _ when PaidAmount > 0 => PurchasePaymentStatus.Partial,
            _ => PurchasePaymentStatus.Unpaid
        };
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
