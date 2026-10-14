using CoCoReTrade.Application;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class UnreadCountsRepository(AppDbContext db) : IUnreadCountsRepository
{
    public Task<int> UnreadNotificationsAsync(string userId, CancellationToken ct) =>
        db.MarketplaceNotifications.AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsRead, ct);

    public Task<int> UnreadMessagesAsync(string userId, CancellationToken ct) =>
        db.ConversationMessages.AsNoTracking()
            .CountAsync(x => x.SenderId != userId && x.ReadAtUtc == null &&
                db.Conversations.Any(c => c.Id == x.ConversationId &&
                    (c.BuyerId == userId || c.SellerId == userId)), ct);
}
