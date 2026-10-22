using CoCoReTrade.Application;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class ShippingZoneRepository(AppDbContext db) : IShippingZoneRepository
{
    public async Task<bool> IsInnerCityAsync(string province, string ward, CancellationToken ct)
    {
        var configured = await db.ShippingZones.AsNoTracking()
            .Where(x => x.Province == province && x.Ward == ward && x.IsActive)
            .Select(x => (bool?)x.IsInnerCity).FirstOrDefaultAsync(ct);
        return configured ?? CoCoReTrade.Domain.ShippingFeeRules.IsDaNang(province);
    }
}
