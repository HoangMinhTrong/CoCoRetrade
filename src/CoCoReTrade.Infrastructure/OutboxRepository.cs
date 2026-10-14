using CoCoReTrade.Application;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class OutboxRepository(AppDbContext db) : IOutboxRepository
{
    public async Task<OutboxDelivery?> ClaimNextAsync(DateTime nowUtc, CancellationToken ct)
    {
        var candidates = await db.OutboxMessages.AsNoTracking()
            .Where(x => x.SentAtUtc == null && x.NextAttemptAtUtc <= nowUtc &&
                (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= nowUtc))
            .OrderBy(x => x.CreatedAtUtc).Select(x => x.Id).Take(20).ToListAsync(ct);
        foreach (var id in candidates)
        {
            var token = Guid.NewGuid();
            var changed = await db.OutboxMessages.Where(x => x.Id == id && x.SentAtUtc == null &&
                    x.NextAttemptAtUtc <= nowUtc && (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= nowUtc))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LeaseToken, token)
                    .SetProperty(x => x.LeaseUntilUtc, nowUtc.AddMinutes(5))
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
            if (changed != 1) continue;
            return await (from outbox in db.OutboxMessages.AsNoTracking()
                          join notification in db.MarketplaceNotifications.AsNoTracking()
                              on outbox.NotificationId equals notification.Id
                          join user in db.Users.AsNoTracking() on notification.UserId equals user.Id
                          where outbox.Id == id
                          select new OutboxDelivery(outbox.Id, token, outbox.Channel, notification.UserId,
                              user.Email, notification.Category, notification.Message, outbox.AttemptCount))
                .SingleAsync(ct);
        }
        return null;
    }

    public async Task MarkSentAsync(Guid id, Guid leaseToken, DateTime nowUtc, CancellationToken ct)
    {
        var changed = await db.OutboxMessages.Where(x => x.Id == id && x.LeaseToken == leaseToken && x.SentAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SentAtUtc, nowUtc)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(x => x.LastError, (string?)null), ct);
        if (changed != 1) throw new InvalidOperationException("Outbox lease was lost after delivery.");
    }

    public async Task DeferAsync(Guid id, Guid leaseToken, DateTime nextAttemptUtc, string? error, CancellationToken ct)
    {
        await db.OutboxMessages.Where(x => x.Id == id && x.LeaseToken == leaseToken && x.SentAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.NextAttemptAtUtc, nextAttemptUtc)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                .SetProperty(x => x.LastError, error == null ? null : error[..Math.Min(error.Length, 300)]), ct);
    }
}
