using RetailShop.Domain.Common;
using RetailShop.Domain.Contacts;

namespace RetailShop.Domain.Sales;

public sealed class Sale : AuditableEntity
{
    private Sale()
    {
    }

    public Sale(
        string invoiceNumber,
        Guid? customerId,
        DateTimeOffset saleDate,
        string? notes)
    {
        InvoiceNumber = invoiceNumber.Trim();
        CustomerId = customerId;
        SaleDate = saleDate;
        Notes = Clean(notes);
    }

    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    public DateTimeOffset SaleDate { get; private set; }
    public SaleStatus Status { get; private set; } = SaleStatus.Completed;
    public SalePaymentStatus PaymentStatus { get; private set; } =
        SalePaymentStatus.Unpaid;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal ReturnedAmount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public decimal DueAmount =>
        Status == SaleStatus.Cancelled
            ? 0
            : Math.Max(0, GrandTotal - ReturnedAmount - (PaidAmount - RefundedAmount));
    public string? Notes { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledOn { get; private set; }
    public ICollection<SaleDetail> Details { get; private set; } = [];
    public ICollection<SalePayment> Payments { get; private set; } = [];

    public void AddDetail(
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal vatAmount,
        decimal costPrice)
    {
        if (Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException("A cancelled sale cannot be changed.");
        }

        Details.Add(new SaleDetail(
            Id,
            productId,
            quantity,
            unitPrice,
            discountAmount,
            vatAmount,
            costPrice));
        RecalculateTotals();
    }

    public void AddPayment(SalePayment payment)
    {
        if (Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException("A cancelled sale cannot receive payments.");
        }
        if (payment.Amount > DueAmount)
        {
            throw new InvalidOperationException(
                "Payment cannot exceed the outstanding sale amount.");
        }

        Payments.Add(payment);
        PaidAmount += payment.Amount;
        UpdatePaymentStatus();
    }

    public void Cancel(Guid performedBy, string reason)
    {
        if (Status == SaleStatus.Cancelled)
        {
            throw new InvalidOperationException("The sale is already cancelled.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A cancellation reason is required.",
                nameof(reason));
        }

        Status = SaleStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledBy = performedBy;
        CancelledOn = DateTimeOffset.UtcNow;
    }

    public void ApplyReturn(decimal amount, decimal refundedAmount)
    {
        if (Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException(
                "Only completed sales can receive returns.");
        }
        if (amount <= 0 || refundedAmount < 0 || refundedAmount > amount)
        {
            throw new InvalidOperationException("Return amounts are invalid.");
        }
        if (ReturnedAmount + amount > GrandTotal)
        {
            throw new InvalidOperationException(
                "Returned amount cannot exceed the sale total.");
        }
        if (RefundedAmount + refundedAmount > PaidAmount)
        {
            throw new InvalidOperationException(
                "Refund cannot exceed the amount paid.");
        }

        ReturnedAmount += amount;
        RefundedAmount += refundedAmount;
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
            0 => SalePaymentStatus.Paid,
            _ when PaidAmount > 0 => SalePaymentStatus.Partial,
            _ => SalePaymentStatus.Unpaid
        };
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
