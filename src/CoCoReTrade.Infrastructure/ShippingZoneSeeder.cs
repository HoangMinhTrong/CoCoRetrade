using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoCoReTrade.Infrastructure;

public static class ShippingZoneSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration configuration, CancellationToken ct = default)
    {
        if (await db.ShippingZones.AnyAsync(ct)) return;
        var configured = configuration.GetSection("Shipping:InnerCityZones").GetChildren()
            .Select(x => new ShippingZone(x["Province"] ?? "", x["Ward"] ?? ""))
            .Where(x => !string.IsNullOrWhiteSpace(x.Province) && !string.IsNullOrWhiteSpace(x.Ward))
            .Distinct().ToList();
        foreach (var zone in configured)
            db.ShippingZones.Add(new ShippingZoneMap
            {
                Province = zone.Province.Trim(), Ward = zone.Ward.Trim(), IsInnerCity = true
            });
        if (configured.Count > 0) await db.SaveChangesAsync(ct);
    }
}
