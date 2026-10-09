using RetailShop.Domain.Returns;
using RetailShop.Domain.Sales;

namespace RetailShop.Tests.Returns;

public sealed class SalesReturnDomainTests
{
    [Fact]
    public void AddDetail_TracksReturnValueAndPreventsDuplicateSaleLines()
    {
        var item = CreateReturn();
        var saleDetailId = Guid.NewGuid();

        item.AddDetail(saleDetailId, 1.5m, 300);

        Assert.Equal(300, item.TotalAmount);
        Assert.Throws<InvalidOperationException>(
            () => item.AddDetail(saleDetailId, 1, 200));
    }

    [Fact]
    public void Resolve_AllowsAuthorizedReviewerToResolveRequest()
    {
        var requester = Guid.NewGuid();
        var item = CreateReturn(requester);
        item.AddDetail(Guid.NewGuid(), 1, 100);

        item.Resolve(requester, 100, 0, Guid.NewGuid(), null);

        Assert.Equal(SalesReturnStatus.Refunded, item.Status);
    }

    [Fact]
    public void Resolve_RecordsRefundAndApprovalHistory()
    {
        var item = CreateReturn();
        item.AddDetail(Guid.NewGuid(), 1, 500);

        item.Resolve(Guid.NewGuid(), 300, 200, Guid.NewGuid(), "Approved");

        Assert.Equal(SalesReturnStatus.Refunded, item.Status);
        Assert.Equal(300, item.RefundedAmount);
        Assert.Equal(200, item.DueAdjustedAmount);
        Assert.Equal(2, item.History.Count);
    }

    [Fact]
    public void Resolve_PreventsAmountsAboveReturnTotal()
    {
        var item = CreateReturn();
        item.AddDetail(Guid.NewGuid(), 1, 500);

        Assert.Throws<InvalidOperationException>(
            () => item.Resolve(Guid.NewGuid(), 400, 101, Guid.NewGuid(), null));
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var requester = Guid.NewGuid();
        var item = CreateReturn(requester);

        Assert.Throws<ArgumentException>(
            () => item.Reject(Guid.NewGuid(), " "));
    }

    [Fact]
    public void SaleApplyReturn_RecalculatesPaidAndDuePosition()
    {
        var sale = new Sale("INV-TEST", Guid.NewGuid(), DateTimeOffset.UtcNow, null);
        sale.AddDetail(Guid.NewGuid(), 1, 1000, 0, 0, 500);
        sale.AddPayment(new SalePayment(
            sale.Id, Guid.NewGuid(), 600, DateTimeOffset.UtcNow,
            Guid.NewGuid(), null, null));

        sale.ApplyReturn(500, 100);

        Assert.Equal(500, sale.ReturnedAmount);
        Assert.Equal(100, sale.RefundedAmount);
        Assert.Equal(0, sale.DueAmount);
        Assert.Equal(SalePaymentStatus.Paid, sale.PaymentStatus);
    }

    private static SalesReturn CreateReturn(Guid? requester = null) =>
        new(
            "RET-TEST",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReturnProductCondition.Available,
            SalesReturnAction.Refund,
            requester ?? Guid.NewGuid(),
            null);
}
