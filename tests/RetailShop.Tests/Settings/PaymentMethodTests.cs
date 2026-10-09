using RetailShop.Domain.Settings;

namespace RetailShop.Tests.Settings;

public sealed class PaymentMethodTests
{
    [Fact]
    public void PaymentMethod_NormalizesNameAndCode()
    {
        var method = new PaymentMethod(" Mobile Banking ", " bkash ", "MobileBanking");

        Assert.Equal("Mobile Banking", method.Name);
        Assert.Equal("MOBILE BANKING", method.NormalizedName);
        Assert.Equal("BKASH", method.Code);
    }
}
