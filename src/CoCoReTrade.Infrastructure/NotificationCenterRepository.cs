using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class NotificationCenterRepository(AppDbContext db) : INotificationCenterRepository
{
    public async Task<IReadOnlyList<MarketplaceNotification>> ForUserAsync(string userId, CancellationToken ct) =>
        await db.MarketplaceNotifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync(ct);
    public Task<MarketplaceNotification?> OwnedAsync(string userId, Guid id, CancellationToken ct) =>
        db.MarketplaceNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
