using RetailShop.Domain.Common;
using RetailShop.Domain.Contacts;
using RetailShop.Domain.Sales;

namespace RetailShop.Domain.Quotations;

public sealed class Quotation : AuditableEntity
{
    private Quotation()
    {
    }

    public Quotation(
        string quotationNumber,
        Guid customerId,
        DateTimeOffset quotationDate,
        DateTimeOffset validUntil,
        string? notes,
        string? terms)
    {
        if (validUntil.Date < quotationDate.Date)
        {
            throw new ArgumentException(
                "Quotation validity cannot end before its issue date.",
                nameof(validUntil));
        }

        QuotationNumber = quotationNumber.Trim();
        CustomerId = customerId;
        QuotationDate = quotationDate;
        ValidUntil = validUntil;
        Notes = Clean(notes);
        Terms = Clean(terms);
    }

    public string QuotationNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public DateTimeOffset QuotationDate { get; private set; }
    public DateTimeOffset ValidUntil { get; private set; }
    public QuotationStatus Status { get; private set; } = QuotationStatus.Draft;
    public decimal Subtotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrandTotal { get; private set; }
    public string? Notes { get; private set; }
    public string? Terms { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? SentOn { get; private set; }
    public DateTimeOffset? AcceptedOn { get; private set; }
    public DateTimeOffset? RejectedOn { get; private set; }
    public DateTimeOffset? ConvertedOn { get; private set; }
    public Guid? ConvertedSaleId { get; private set; }
    public Sale? ConvertedSale { get; private set; }
    public ICollection<QuotationDetail> Details { get; private set; } = [];

    public void AddDetail(
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal vatAmount)
    {
        EnsureDraft();
        Details.Add(new QuotationDetail(
            Id,
            productId,
            quantity,
            unitPrice,
            discountAmount,
            vatAmount));
        RecalculateTotals();
    }

    public void ReplaceDetails(IEnumerable<QuotationDetail> details)
    {
        EnsureDraft();
        Details.Clear();
        foreach (var detail in details)
        {
            Details.Add(detail);
        }
        RecalculateTotals();
    }

    public void Update(
        Guid customerId,
        DateTimeOffset quotationDate,
        DateTimeOffset validUntil,
        string? notes,
        string? terms)
    {
        EnsureDraft();
        if (validUntil.Date < quotationDate.Date)
        {
            throw new InvalidOperationException(
                "Quotation validity cannot end before its issue date.");
        }

        CustomerId = customerId;
        QuotationDate = quotationDate;
        ValidUntil = validUntil;
        Notes = Clean(notes);
        Terms = Clean(terms);
    }

    public void Send()
    {
        EnsureNotExpired();
        if (Status != QuotationStatus.Draft || Details.Count == 0)
        {
            throw new InvalidOperationException(
                "Only a populated draft quotation can be sent.");
        }
        Status = QuotationStatus.Sent;
        SentOn = DateTimeOffset.UtcNow;
    }

    public void Accept()
    {
        EnsureNotExpired();
        if (Status != QuotationStatus.Sent)
        {
            throw new InvalidOperationException(
                "Only a sent quotation can be accepted.");
        }
        Status = QuotationStatus.Accepted;
        AcceptedOn = DateTimeOffset.UtcNow;
    }

    public void Reject(string reason)
    {
        if (Status != QuotationStatus.Sent)
        {
            throw new InvalidOperationException(
                "Only a sent quotation can be rejected.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A rejection reason is required.", nameof(reason));
        }

        Status = QuotationStatus.Rejected;
        RejectionReason = reason.Trim();
        RejectedOn = DateTimeOffset.UtcNow;
    }

    public void Expire()
    {
        if (Status is QuotationStatus.Converted or QuotationStatus.Rejected)
        {
            throw new InvalidOperationException(
                "This quotation is already in a terminal state.");
        }
        Status = QuotationStatus.Expired;
    }

    public void Convert(Guid saleId)
    {
        EnsureNotExpired();
        if (Status != QuotationStatus.Accepted)
        {
            throw new InvalidOperationException(
                "Only an accepted quotation can be converted.");
        }

        Status = QuotationStatus.Converted;
        ConvertedSaleId = saleId;
        ConvertedOn = DateTimeOffset.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != QuotationStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only a draft quotation can be edited.");
        }
    }

    private void EnsureNotExpired()
    {
        if (ValidUntil.Date < DateTimeOffset.UtcNow.Date)
        {
            Status = QuotationStatus.Expired;
            throw new InvalidOperationException("The quotation has expired.");
        }
    }

    private void RecalculateTotals()
    {
        Subtotal = Details.Sum(detail => detail.GrossAmount);
        DiscountAmount = Details.Sum(detail => detail.DiscountAmount);
        VatAmount = Details.Sum(detail => detail.VatAmount);
        GrandTotal = Details.Sum(detail => detail.LineTotal);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
