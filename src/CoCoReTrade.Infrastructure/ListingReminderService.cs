using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoCoReTrade.Infrastructure;

public sealed class ListingReminderService(AppDbContext db, TimeProvider clock,
    IConfiguration configuration) : IListingReminderService
{
    public async Task<int> SendDueAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var afterDays = configuration.GetValue<int?>("Listings:ReminderAfterDays") ?? 30;
        if (afterDays is < 7 or > 365)
            throw new InvalidOperationException("Listings:ReminderAfterDays must be between 7 and 365.");
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-afterDays);
        var due = await db.Products.AsNoTracking()
            .Where(x => x.Status == ProductStatus.Active && x.ReminderSentAtUtc == null &&
                        x.PublishedAtUtc <= cutoff)
            .OrderBy(x => x.PublishedAtUtc)
            .Select(x => new { x.Id, x.SellerId, x.Name })
            .Take(batchSize).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var changed = await db.Products.Where(x => x.Id == item.Id &&
                    x.Status == ProductStatus.Active && x.ReminderSentAtUtc == null &&
                    x.PublishedAtUtc <= cutoff)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.ReminderSentAtUtc, now), cancellationToken);
            if (changed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }
            db.MarketplaceNotifications.Add(new MarketplaceNotification
            {
                Id = Guid.NewGuid(), UserId = item.SellerId, Category = "Listings",
                ProductId = item.Id,
                Message = $"Sản phẩm {item.Name} đã mở bán {afterDays} ngày. Bạn có thể liên hệ CoCo để xem lại giá.",
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            sent++;
        }
        return sent;
    }
}
