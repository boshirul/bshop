using RetailShop.Shared.Contracts;

namespace RetailShop.Tests.Shared;

public sealed class ApiResponseTests
{
    [Fact]
    public void Success_ContainsDataWithoutErrors()
    {
        var response = ApiResponse<string>.Success("ready");

        Assert.True(response.Succeeded);
        Assert.Equal("ready", response.Data);
        Assert.Empty(response.Errors);
    }

    [Fact]
    public void Failure_ContainsErrorsWithoutData()
    {
        var response = ApiResponse<string>.Failure(["invalid"]);

        Assert.False(response.Succeeded);
        Assert.Null(response.Data);
        Assert.Equal(["invalid"], response.Errors);
    }
}
