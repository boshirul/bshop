using RetailShop.Domain.Quotations;

namespace RetailShop.Tests.Quotations;

public sealed class QuotationDomainTests
{
    [Fact]
    public void AddDetail_CalculatesTotals()
    {
        var quotation = CreateQuotation();

        quotation.AddDetail(
            Guid.NewGuid(),
            quantity: 2,
            unitPrice: 100,
            discountAmount: 10,
            vatAmount: 9.50m);
        quotation.AddDetail(
            Guid.NewGuid(),
            quantity: 3,
            unitPrice: 50,
            discountAmount: 0,
            vatAmount: 7.50m);

        Assert.Equal(350, quotation.Subtotal);
        Assert.Equal(10, quotation.DiscountAmount);
        Assert.Equal(17, quotation.VatAmount);
        Assert.Equal(357, quotation.GrandTotal);
    }

    [Fact]
    public void Constructor_PreventsValidityBeforeIssueDate()
    {
        var issueDate = DateTimeOffset.UtcNow.Date;

        Assert.Throws<ArgumentException>(
            () => new Quotation(
                "QUO-TEST",
                Guid.NewGuid(),
                issueDate,
                issueDate.AddDays(-1),
                null,
                null));
    }

    [Fact]
    public void Draft_CanBeUpdatedAndTrimsOptionalText()
    {
        var quotation = CreateQuotation();
        var customerId = Guid.NewGuid();
        var issueDate = DateTimeOffset.UtcNow.AddDays(1);
        var validUntil = issueDate.AddDays(14);

        quotation.Update(
            customerId,
            issueDate,
            validUntil,
            " Updated notes ",
            " Net seven days ");

        Assert.Equal(customerId, quotation.CustomerId);
        Assert.Equal(issueDate, quotation.QuotationDate);
        Assert.Equal(validUntil, quotation.ValidUntil);
        Assert.Equal("Updated notes", quotation.Notes);
        Assert.Equal("Net seven days", quotation.Terms);
    }

    [Fact]
    public void Send_RequiresPopulatedUnexpiredDraft()
    {
        var empty = CreateQuotation();
        var expired = CreateQuotation(
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));
        expired.AddDetail(Guid.NewGuid(), 1, 100, 0, 0);

        Assert.Throws<InvalidOperationException>(() => empty.Send());
        Assert.Equal(QuotationStatus.Draft, empty.Status);

        Assert.Throws<InvalidOperationException>(() => expired.Send());
        Assert.Equal(QuotationStatus.Expired, expired.Status);
    }

    [Fact]
    public void SentQuotation_CannotBeEdited()
    {
        var quotation = CreatePopulatedQuotation();
        quotation.Send();

        Assert.Equal(QuotationStatus.Sent, quotation.Status);
        Assert.NotNull(quotation.SentOn);
        Assert.Throws<InvalidOperationException>(
            () => quotation.AddDetail(Guid.NewGuid(), 1, 10, 0, 0));
        Assert.Throws<InvalidOperationException>(
            () => quotation.Update(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(10),
                null,
                null));
        Assert.Throws<InvalidOperationException>(
            () => quotation.ReplaceDetails([]));
    }

    [Fact]
    public void Accept_RequiresSentQuotationAndRecordsAcceptance()
    {
        var quotation = CreatePopulatedQuotation();

        Assert.Throws<InvalidOperationException>(() => quotation.Accept());

        quotation.Send();
        quotation.Accept();

        Assert.Equal(QuotationStatus.Accepted, quotation.Status);
        Assert.NotNull(quotation.AcceptedOn);
        Assert.Throws<InvalidOperationException>(() => quotation.Accept());
    }

    [Fact]
    public void Reject_RequiresSentQuotationAndReason()
    {
        var quotation = CreatePopulatedQuotation();
        quotation.Send();

        Assert.Throws<ArgumentException>(() => quotation.Reject(" "));
        Assert.Equal(QuotationStatus.Sent, quotation.Status);

        quotation.Reject(" Customer declined ");

        Assert.Equal(QuotationStatus.Rejected, quotation.Status);
        Assert.Equal("Customer declined", quotation.RejectionReason);
        Assert.NotNull(quotation.RejectedOn);
        Assert.Throws<InvalidOperationException>(() => quotation.Accept());
    }

    [Fact]
    public void Expire_MarksQuotationAndTerminalStatesCannotBeExpired()
    {
        var quotation = CreatePopulatedQuotation();

        quotation.Expire();

        Assert.Equal(QuotationStatus.Expired, quotation.Status);
        Assert.Throws<InvalidOperationException>(() => quotation.Send());

        var rejected = CreatePopulatedQuotation();
        rejected.Send();
        rejected.Reject("Not required");
        Assert.Throws<InvalidOperationException>(() => rejected.Expire());
    }

    [Fact]
    public void Convert_RequiresAcceptedQuotationAndCanOnlyHappenOnce()
    {
        var quotation = CreatePopulatedQuotation();
        var saleId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(
            () => quotation.Convert(saleId));

        quotation.Send();
        quotation.Accept();
        quotation.Convert(saleId);

        Assert.Equal(QuotationStatus.Converted, quotation.Status);
        Assert.Equal(saleId, quotation.ConvertedSaleId);
        Assert.NotNull(quotation.ConvertedOn);

        Assert.Throws<InvalidOperationException>(
            () => quotation.Convert(Guid.NewGuid()));
        Assert.Equal(saleId, quotation.ConvertedSaleId);
        Assert.Throws<InvalidOperationException>(() => quotation.Expire());
    }

    private static Quotation CreatePopulatedQuotation()
    {
        var quotation = CreateQuotation();
        quotation.AddDetail(Guid.NewGuid(), 2, 100, 10, 5);
        return quotation;
    }

    private static Quotation CreateQuotation() =>
        CreateQuotation(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30));

    private static Quotation CreateQuotation(
        DateTimeOffset quotationDate,
        DateTimeOffset validUntil) =>
        new(
            "QUO-TEST",
            Guid.NewGuid(),
            quotationDate,
            validUntil,
            " Test quotation ",
            " Payment due on acceptance ");
}
