using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class NotificationCenterRepository(AppDbContext db) : INotificationCenterRepository
{
    public async Task<IReadOnlyList<MarketplaceNotification>> ForUserAsync(string userId, CancellationToken ct)
    {
        var notifications = await db.MarketplaceNotifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync(ct);
        await NotificationProductNames.ApplyAsync(db, notifications, ct);
        return notifications;
    }
    public async Task<MarketplaceNotification?> OwnedAsync(string userId, Guid id, CancellationToken ct)
    {
        var notification = await db.MarketplaceNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (notification is not null)
            await NotificationProductNames.ApplyAsync(db, [notification], ct);
        return notification;
    }
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
