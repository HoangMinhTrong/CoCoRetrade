using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class PlatformCommissionRepository(AppDbContext db) : IPlatformCommissionRepository
{
    public Task<bool> HasEntryAsync(Guid orderId, CancellationToken ct) =>
        db.PlatformCommissionEntries.AnyAsync(x => x.OrderId == orderId, ct);

    public void Add(PlatformCommissionEntry entry) => db.PlatformCommissionEntries.Add(entry);

    public async Task<PlatformCommissionWallet> SnapshotAsync(CancellationToken ct)
    {
        var entries = db.PlatformCommissionEntries.AsNoTracking();
        var balance = await entries.Select(x => (decimal?)x.Amount).SumAsync(ct) ?? 0m;
        var count = await entries.CountAsync(ct);
        var recent = await (from entry in entries
                            join order in db.Orders.AsNoTracking() on entry.OrderId equals order.Id
                            orderby entry.CreatedAtUtc descending
                            select new PlatformCommissionRow(entry.OrderId, order.ProductSku,
                                order.ItemAmount, entry.Amount, entry.CreatedAtUtc))
            .Take(100).ToListAsync(ct);
        return new PlatformCommissionWallet(balance, count, recent);
    }
}
