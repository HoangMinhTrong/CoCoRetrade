using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Tests;

public sealed class ShippingFeeRulesTests
{
    [Theory]
    [InlineData("Đà Nẵng")]
    [InlineData("Da Nang")]
    [InlineData("Thành phố Đà Nẵng")]
    public void DaNangUsesInnerCityFeeWhenNoWardConfigurationExists(string province)
    {
        var policy = new ShippingPolicy();
        Assert.True(ShippingFeeRules.IsDaNang(province));
        Assert.Equal(20_000m, policy.Fee(FulfillmentType.Shipping, province, "Sơn Trà"));
    }

    [Fact]
    public void OtherProvincesUseOuterCityFeeAndPickupIsFree()
    {
        var policy = new ShippingPolicy();
        Assert.Equal(35_000m, policy.Fee(FulfillmentType.Shipping, "Hà Nội", "Cầu Giấy"));
        Assert.Equal(0m, policy.Fee(FulfillmentType.Pickup, null, null));
    }
}
