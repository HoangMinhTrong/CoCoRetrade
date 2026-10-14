using CoCoReTrade.Application;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class ShippingZoneRepository(AppDbContext db) : IShippingZoneRepository
{
    public Task<bool> IsInnerCityAsync(string province, string ward, CancellationToken ct) =>
        db.ShippingZones.AsNoTracking().AnyAsync(x => x.Province == province && x.Ward == ward &&
            x.IsActive && x.IsInnerCity, ct);
}
