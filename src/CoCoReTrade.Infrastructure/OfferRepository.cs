using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class OfferRepository(AppDbContext db) : IOfferRepository
{
    public async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }
    public Task<Product?> ProductAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Offer?> OfferAsync(Guid id, CancellationToken ct) =>
        db.Offers.Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Offer>> ForUserAsync(string userId, CancellationToken ct) =>
        await db.Offers.AsNoTracking().Include(x => x.Events)
            .Where(x => x.BuyerId == userId || x.SellerId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
    public Task<int> BuyerPriceCountAsync(Guid productId, string buyerId, CancellationToken ct) =>
        db.OfferEvents.CountAsync(e =>
            (e.Action == OfferEventAction.Submitted || e.Action == OfferEventAction.BuyerCountered) &&
            db.Offers.Any(o => o.Id == e.OfferId && o.ProductId == productId && o.BuyerId == buyerId), ct);
    public async Task<IReadOnlyList<Guid>> DueForExpirationAsync(DateTime nowUtc, int batchSize, CancellationToken ct) =>
        await db.Offers.AsNoTracking()
            .Where(x => (x.Status == OfferStatus.PendingSeller || x.Status == OfferStatus.PendingBuyer ||
                         x.Status == OfferStatus.Accepted) && x.ExpiresAtUtc <= nowUtc)
            .OrderBy(x => x.ExpiresAtUtc).Select(x => x.Id).Take(batchSize).ToListAsync(ct);
    public async Task AddOfferAsync(Offer offer, CancellationToken ct) => await db.Offers.AddAsync(offer, ct);
    public void AddEvent(OfferEvent offerEvent) => db.OfferEvents.Add(offerEvent);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public async Task<IReadOnlyList<MarketplaceNotification>> NotificationsAsync(string userId, CancellationToken ct)
    {
        var notifications = await db.MarketplaceNotifications.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
        await NotificationProductNames.ApplyAsync(db, notifications, ct);
        return notifications;
    }
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
