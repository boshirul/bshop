using RetailShop.Domain.Purchases;

namespace RetailShop.Tests.Purchases;

public sealed class PurchaseDomainTests
{
    [Fact]
    public void AddDetail_CalculatesPurchaseTotals()
    {
        var purchase = CreatePurchase();

        purchase.AddDetail(
            Guid.NewGuid(),
            quantity: 2,
            unitCost: 100,
            discountAmount: 10,
            vatAmount: 9.50m);
        purchase.AddDetail(
            Guid.NewGuid(),
            quantity: 3,
            unitCost: 50,
            discountAmount: 0,
            vatAmount: 7.50m);

        Assert.Equal(350, purchase.Subtotal);
        Assert.Equal(10, purchase.DiscountAmount);
        Assert.Equal(17, purchase.VatAmount);
        Assert.Equal(357, purchase.GrandTotal);
        Assert.Equal(357, purchase.DueAmount);
        Assert.Equal(PurchasePaymentStatus.Unpaid, purchase.PaymentStatus);
    }

    [Fact]
    public void AddPayment_UpdatesPaidDueAndPaymentStatus()
    {
        var purchase = CreatePurchase();
        purchase.AddDetail(Guid.NewGuid(), 2, 100, 0, 0);

        purchase.AddPayment(CreatePayment(purchase.Id, 75));

        Assert.Equal(75, purchase.PaidAmount);
        Assert.Equal(125, purchase.DueAmount);
        Assert.Equal(PurchasePaymentStatus.Partial, purchase.PaymentStatus);

        purchase.AddPayment(CreatePayment(purchase.Id, 125));

        Assert.Equal(0, purchase.DueAmount);
        Assert.Equal(PurchasePaymentStatus.Paid, purchase.PaymentStatus);
    }

    [Fact]
    public void AddPayment_PreventsOverpayment()
    {
        var purchase = CreatePurchase();
        purchase.AddDetail(Guid.NewGuid(), 1, 100, 0, 0);

        var exception = Assert.Throws<InvalidOperationException>(
            () => purchase.AddPayment(CreatePayment(purchase.Id, 100.01m)));

        Assert.Contains("cannot exceed", exception.Message);
        Assert.Empty(purchase.Payments);
        Assert.Equal(0, purchase.PaidAmount);
    }

    [Fact]
    public void PurchaseDetail_RegisterReturn_PreventsReturningMoreThanPurchased()
    {
        var detail = new PurchaseDetail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            quantity: 5,
            unitCost: 20,
            discountAmount: 10,
            vatAmount: 5);

        var returnAmount = detail.RegisterReturn(3);

        Assert.Equal(57, returnAmount);
        Assert.Equal(3, detail.ReturnedQuantity);
        Assert.Throws<InvalidOperationException>(() => detail.RegisterReturn(3));
        Assert.Equal(3, detail.ReturnedQuantity);
    }

    [Fact]
    public void RegisterReturn_UpdatesPurchaseStatusAndOutstandingAmount()
    {
        var purchase = CreatePurchase();
        purchase.AddDetail(Guid.NewGuid(), 2, 100, 0, 0);

        purchase.RegisterReturn(50);

        Assert.Equal(50, purchase.ReturnedAmount);
        Assert.Equal(150, purchase.DueAmount);
        Assert.Equal(PurchaseStatus.PartiallyReturned, purchase.Status);

        purchase.RegisterReturn(150);

        Assert.Equal(0, purchase.DueAmount);
        Assert.Equal(PurchaseStatus.Returned, purchase.Status);
        Assert.Equal(PurchasePaymentStatus.Paid, purchase.PaymentStatus);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 5)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void SupplierLedgerEntry_RequiresExactlyOnePositiveSide(
        decimal debit,
        decimal credit)
    {
        Assert.Throws<InvalidOperationException>(
            () => CreateLedgerEntry(debit, credit));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    public void SupplierLedgerEntry_AcceptsDebitOrCredit(
        decimal debit,
        decimal credit)
    {
        var entry = CreateLedgerEntry(debit, credit);

        Assert.Equal(debit, entry.Debit);
        Assert.Equal(credit, entry.Credit);
    }

    private static Purchase CreatePurchase() =>
        new(
            "PUR-TEST",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            " SUP-001 ",
            " Test purchase ");

    private static PurchasePayment CreatePayment(Guid purchaseId, decimal amount) =>
        new(
            purchaseId,
            Guid.NewGuid(),
            amount,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            null,
            null);

    private static SupplierLedgerEntry CreateLedgerEntry(
        decimal debit,
        decimal credit) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            debit > 0
                ? SupplierLedgerEntryType.Purchase
                : SupplierLedgerEntryType.Payment,
            debit,
            credit,
            "Purchase",
            Guid.NewGuid(),
            "PUR-TEST",
            Guid.NewGuid(),
            null);
}
