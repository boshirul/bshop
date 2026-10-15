using RetailShop.Domain.Expenses;

namespace RetailShop.Tests.Expenses;

public sealed class ExpenseVoucherDomainTests
{
    [Fact]
    public void Draft_voucher_calculates_its_total_from_multiple_lines_and_posts_once()
    {
        var category = Guid.CreateVersion7();
        var voucher = new ExpenseVoucher("EXP-TEST", new DateOnly(2026, 10, 15), "Utility provider", null, null, Guid.CreateVersion7());
        voucher.ReplaceDetails([
            new ExpenseVoucherDetail(category, "Electricity", 1200m, null),
            new ExpenseVoucherDetail(category, "Internet", 800m, null)
        ]);

        Assert.Equal(2000m, voucher.TotalAmount);
        voucher.Post(Guid.CreateVersion7());
        Assert.Equal(ExpenseVoucherStatus.Posted, voucher.Status);
        Assert.Throws<InvalidOperationException>(() => voucher.Post(Guid.CreateVersion7()));
    }

    [Fact]
    public void Posted_voucher_requires_a_reason_to_void()
    {
        var voucher = new ExpenseVoucher("EXP-TEST", new DateOnly(2026, 10, 15), "Payee", null, null, Guid.CreateVersion7());
        voucher.ReplaceDetails([new ExpenseVoucherDetail(Guid.CreateVersion7(), "Repair", 1m, null)]);
        voucher.Post(Guid.CreateVersion7());
        Assert.Throws<InvalidOperationException>(() => voucher.Void(Guid.CreateVersion7(), ""));
        voucher.Void(Guid.CreateVersion7(), "Entry recorded in error");
        Assert.Equal(ExpenseVoucherStatus.Voided, voucher.Status);
    }
}
