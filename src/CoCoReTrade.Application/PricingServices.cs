using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public interface IShippingZoneRepository
{
    Task<bool> IsInnerCityAsync(string province, string ward, CancellationToken ct);
}

public interface IShippingFeeService
{
    Task<decimal> FeeAsync(FulfillmentType fulfillment, string? province, string? ward, CancellationToken ct);
}

public sealed class ShippingFeeService(IShippingZoneRepository zones) : IShippingFeeService
{
    public async Task<decimal> FeeAsync(FulfillmentType fulfillment, string? province, string? ward, CancellationToken ct)
    {
        if (fulfillment == FulfillmentType.Pickup) return 0;
        if (fulfillment != FulfillmentType.Shipping) throw new ArgumentOutOfRangeException(nameof(fulfillment));
        if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(ward))
            throw new ArgumentException("Province/city and ward/commune are required.");
        return ShippingFeeRules.Calculate(fulfillment,
            await zones.IsInnerCityAsync(province.Trim(), ward.Trim(), ct));
    }
}

public interface ICommissionService
{
    decimal Calculate(decimal merchandisePrice);
}

public sealed class CommissionService(MarketplacePolicy policy) : ICommissionService
{
    public decimal Calculate(decimal merchandisePrice) =>
        CommissionRules.Calculate(merchandisePrice, policy.CommissionPercent);
}
