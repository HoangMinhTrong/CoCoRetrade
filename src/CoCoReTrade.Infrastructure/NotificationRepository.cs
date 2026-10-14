using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Infrastructure;

public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task AddAsync(MarketplaceNotification notification, CancellationToken ct)
    {
        await db.MarketplaceNotifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);
    }
}
