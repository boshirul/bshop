using RetailShop.Domain.Inventory;

namespace RetailShop.Tests.Inventory;

public sealed class StockAdjustmentTests
{
    [Fact]
    public void Approval_TransitionsPendingAdjustment()
    {
        var adjustment = new StockAdjustment(
            "ADJ-TEST",
            "Physical count correction",
            Guid.NewGuid());

        adjustment.Approve(Guid.NewGuid(), "Count verified.");

        Assert.Equal(StockAdjustmentStatus.Approved, adjustment.Status);
        Assert.NotNull(adjustment.ReviewedOn);
    }

    [Fact]
    public void RejectedAdjustment_CannotBeApproved()
    {
        var adjustment = new StockAdjustment(
            "ADJ-TEST",
            "Physical count correction",
            Guid.NewGuid());
        adjustment.Reject(Guid.NewGuid(), "Count not verified.");

        Assert.Throws<InvalidOperationException>(
            () => adjustment.Approve(Guid.NewGuid(), null));
    }

    [Fact]
    public void ApprovedAdjustment_CanBeReversedOnce()
    {
        var adjustment = new StockAdjustment(
            "ADJ-TEST",
            "Physical count correction",
            Guid.NewGuid());
        adjustment.Approve(Guid.NewGuid(), null);

        adjustment.Reverse(Guid.NewGuid(), "Entry posted to wrong bucket.");

        Assert.Equal(StockAdjustmentStatus.Reversed, adjustment.Status);
        Assert.Throws<InvalidOperationException>(
            () => adjustment.Reverse(Guid.NewGuid(), "Again"));
    }
}
