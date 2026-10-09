using RetailShop.Domain.Sales;

namespace RetailShop.Tests.Sales;

public sealed class SaleDomainTests
{
    [Fact]
    public void AddDetail_CalculatesTotalsAndProfit()
    {
        var sale = CreateSale();

        sale.AddDetail(
            Guid.NewGuid(),
            quantity: 2,
            unitPrice: 100,
            discountAmount: 10,
            vatAmount: 9.50m,
            costPrice: 60);
        sale.AddDetail(
            Guid.NewGuid(),
            quantity: 3,
            unitPrice: 50,
            discountAmount: 0,
            vatAmount: 7.50m,
            costPrice: 30);

        Assert.Equal(350, sale.Subtotal);
        Assert.Equal(10, sale.DiscountAmount);
        Assert.Equal(17, sale.VatAmount);
        Assert.Equal(357, sale.GrandTotal);
        Assert.Equal(357, sale.DueAmount);
        Assert.Equal(147, sale.Details.Sum(item => item.Profit));
    }

    [Fact]
    public void SaleDetail_PreventsDiscountAboveGrossAmount()
    {
        Assert.Throws<InvalidOperationException>(
            () => new SaleDetail(
                Guid.NewGuid(),
                Guid.NewGuid(),
                quantity: 2,
                unitPrice: 50,
                discountAmount: 100.01m,
                vatAmount: 0,
                costPrice: 25));
    }

    [Fact]
    public void AddPayment_UpdatesPaidDueAndPaymentStatus()
    {
        var sale = CreateSale();
        sale.AddDetail(Guid.NewGuid(), 2, 100, 0, 0, 50);

        sale.AddPayment(CreatePayment(sale.Id, 75));

        Assert.Equal(75, sale.PaidAmount);
        Assert.Equal(125, sale.DueAmount);
        Assert.Equal(SalePaymentStatus.Partial, sale.PaymentStatus);

        sale.AddPayment(CreatePayment(sale.Id, 125));

        Assert.Equal(200, sale.PaidAmount);
        Assert.Equal(0, sale.DueAmount);
        Assert.Equal(SalePaymentStatus.Paid, sale.PaymentStatus);
    }

    [Fact]
    public void AddPayment_PreventsOverpaymentWithoutChangingSale()
    {
        var sale = CreateSale();
        sale.AddDetail(Guid.NewGuid(), 1, 100, 0, 0, 50);

        var exception = Assert.Throws<InvalidOperationException>(
            () => sale.AddPayment(CreatePayment(sale.Id, 100.01m)));

        Assert.Contains("cannot exceed", exception.Message);
        Assert.Empty(sale.Payments);
        Assert.Equal(0, sale.PaidAmount);
        Assert.Equal(100, sale.DueAmount);
    }

    [Fact]
    public void Cancel_RequiresReason()
    {
        var sale = CreateSale();

        Assert.Throws<ArgumentException>(
            () => sale.Cancel(Guid.NewGuid(), " "));
        Assert.Equal(SaleStatus.Completed, sale.Status);
    }

    [Fact]
    public void Cancel_RecordsActorAndReasonAndCanOnlyHappenOnce()
    {
        var sale = CreateSale();
        var actor = Guid.NewGuid();

        sale.Cancel(actor, " Customer changed mind ");

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(actor, sale.CancelledBy);
        Assert.Equal("Customer changed mind", sale.CancellationReason);
        Assert.NotNull(sale.CancelledOn);
        Assert.Throws<InvalidOperationException>(
            () => sale.Cancel(actor, "Again"));
    }

    [Fact]
    public void CancelledSale_CannotReceiveItemsOrPayments()
    {
        var sale = CreateSale();
        sale.AddDetail(Guid.NewGuid(), 1, 100, 0, 0, 50);
        sale.Cancel(Guid.NewGuid(), "Voided before delivery");

        Assert.Throws<InvalidOperationException>(
            () => sale.AddDetail(Guid.NewGuid(), 1, 10, 0, 0, 5));
        Assert.Throws<InvalidOperationException>(
            () => sale.AddPayment(CreatePayment(sale.Id, 10)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 5)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void CustomerLedgerEntry_RequiresExactlyOnePositiveSide(
        decimal debit,
        decimal credit)
    {
        Assert.Throws<InvalidOperationException>(
            () => CreateLedgerEntry(debit, credit));
    }

    private static Sale CreateSale() =>
        new(
            "INV-TEST",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            " Test sale ");

    private static SalePayment CreatePayment(Guid saleId, decimal amount) =>
        new(
            saleId,
            Guid.NewGuid(),
            amount,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            null,
            null);

    private static CustomerLedgerEntry CreateLedgerEntry(
        decimal debit,
        decimal credit) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            debit > 0
                ? CustomerLedgerEntryType.Sale
                : CustomerLedgerEntryType.Payment,
            debit,
            credit,
            "Sale",
            Guid.NewGuid(),
            "INV-TEST",
            Guid.NewGuid(),
            null);
}
