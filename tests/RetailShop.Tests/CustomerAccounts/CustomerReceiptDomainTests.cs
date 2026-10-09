using RetailShop.Domain.CustomerAccounts;

namespace RetailShop.Tests.CustomerAccounts;

public sealed class CustomerReceiptDomainTests
{
    [Fact]
    public void Allocate_TracksAllocationTotalAndRemainingAccountCredit()
    {
        var receipt = CreateReceipt(amount: 500);

        receipt.Allocate(Guid.NewGuid(), 125);
        receipt.Allocate(Guid.NewGuid(), 175);

        Assert.Equal(300, receipt.AllocatedAmount);
        Assert.Equal(200, receipt.AccountAppliedAmount);
        Assert.Collection(
            receipt.Allocations,
            allocation => Assert.Equal(125, allocation.Amount),
            allocation => Assert.Equal(175, allocation.Amount));
    }

    [Fact]
    public void Allocate_CanApplyTheFullReceiptWithoutLeavingABalance()
    {
        var receipt = CreateReceipt(amount: 500);

        receipt.Allocate(Guid.NewGuid(), 500);

        Assert.Equal(receipt.Amount, receipt.AllocatedAmount);
        Assert.Equal(0, receipt.AccountAppliedAmount);
    }

    [Fact]
    public void Allocate_PreventsOverAllocationWithoutChangingReceipt()
    {
        var receipt = CreateReceipt(amount: 500);
        receipt.Allocate(Guid.NewGuid(), 400);

        var exception = Assert.Throws<InvalidOperationException>(
            () => receipt.Allocate(Guid.NewGuid(), 100.01m));

        Assert.Contains("cannot exceed", exception.Message);
        Assert.Single(receipt.Allocations);
        Assert.Equal(400, receipt.AllocatedAmount);
        Assert.Equal(100, receipt.AccountAppliedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Allocate_RequiresAPositiveAmount(decimal amount)
    {
        var receipt = CreateReceipt(amount: 500);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => receipt.Allocate(Guid.NewGuid(), amount));
        Assert.Empty(receipt.Allocations);
        Assert.Equal(0, receipt.AllocatedAmount);
    }

    [Fact]
    public void Allocate_PreventsAllocatingTheSameInvoiceTwice()
    {
        var receipt = CreateReceipt(amount: 500);
        var saleId = Guid.NewGuid();
        receipt.Allocate(saleId, 100);

        var exception = Assert.Throws<InvalidOperationException>(
            () => receipt.Allocate(saleId, 50));

        Assert.Contains("only once", exception.Message);
        Assert.Single(receipt.Allocations);
        Assert.Equal(100, receipt.AllocatedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Receipt_RequiresAPositiveAmount(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateReceipt(amount));
    }

    [Fact]
    public void ReceiptAndAllocationFinancialValuesHaveNoPublicSetters()
    {
        AssertNoPublicSetter<CustomerReceipt>(nameof(CustomerReceipt.Amount));
        AssertNoPublicSetter<CustomerReceipt>(nameof(CustomerReceipt.AllocatedAmount));
        AssertNoPublicSetter<CustomerReceipt>(nameof(CustomerReceipt.ReceivedOn));
        AssertNoPublicSetter<CustomerReceiptAllocation>(
            nameof(CustomerReceiptAllocation.Amount));
        AssertNoPublicSetter<CustomerReceiptAllocation>(
            nameof(CustomerReceiptAllocation.SaleId));
        AssertNoPublicSetter<CustomerReceiptAllocation>(
            nameof(CustomerReceiptAllocation.CustomerReceiptId));
    }

    [Fact]
    public void Receipt_CleansReferenceAndNotesAtCreation()
    {
        var receipt = new CustomerReceipt(
            " RCP-TEST ",
            Guid.NewGuid(),
            Guid.NewGuid(),
            500,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            " BANK-42 ",
            " Customer settled account ");

        Assert.Equal("RCP-TEST", receipt.ReceiptNumber);
        Assert.Equal("BANK-42", receipt.ReferenceNumber);
        Assert.Equal("Customer settled account", receipt.Notes);
    }

    private static CustomerReceipt CreateReceipt(decimal amount = 500) =>
        new(
            "RCP-TEST",
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            null,
            null);

    private static void AssertNoPublicSetter<T>(string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.SetMethod?.IsPublic ?? false);
    }
}
