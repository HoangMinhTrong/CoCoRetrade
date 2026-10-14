using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class SocialRepository(AppDbContext db) : ISocialRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }
    public Task<Product?> ProductAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> SellerExistsAsync(string id, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == id &&
            (x.LockoutEnd == null || x.LockoutEnd <= DateTimeOffset.UtcNow), ct);
    public Task<string?> SellerNameAsync(string id, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.FullName).FirstOrDefaultAsync(ct);
    public Task<bool> IsFavoriteAsync(string userId, Guid productId, CancellationToken ct) =>
        db.Favorites.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ProductId == productId, ct);
    public Task<bool> IsFollowingAsync(string followerId, string sellerId, CancellationToken ct) =>
        db.SellerFollows.AsNoTracking().AnyAsync(x => x.FollowerId == followerId && x.SellerId == sellerId, ct);
    public async Task<IReadOnlyList<Guid>> FavoriteIdsAsync(string userId, CancellationToken ct) =>
        await db.Favorites.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => x.ProductId).ToListAsync(ct);
    public async Task<IReadOnlyList<string>> FollowingIdsAsync(string userId, CancellationToken ct) =>
        await db.SellerFollows.AsNoTracking().Where(x => x.FollowerId == userId)
            .Select(x => x.SellerId).ToListAsync(ct);
    public async Task<IReadOnlyList<ProductCard>> SavedProductsAsync(string userId, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().Include(x => x.Images)
            .Where(x => x.Status == ProductStatus.Active &&
                db.Favorites.Any(f => f.UserId == userId && f.ProductId == x.Id))
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
        var sellerIds = products.Select(x => x.SellerId).Distinct().ToList();
        var productIds = products.Select(x => x.Id).ToList();
        var names = await db.Users.AsNoTracking().Where(x => sellerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName, HasAvatar = x.AvatarStorageKey != null })
            .ToDictionaryAsync(x => x.Id, ct);
        var likes = await db.Favorites.AsNoTracking().Where(x => productIds.Contains(x.ProductId))
            .GroupBy(x => x.ProductId).Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return products.Select(x => new ProductCard(x.Id, x.Sku, x.Name, x.Brand, x.Category,
            x.Size, x.AskingPrice, x.VerifiedCondition ?? x.DeclaredCondition, x.Status,
            x.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Id, x.SellerId, x.ConsignmentCode,
            names.TryGetValue(x.SellerId, out var seller) ? seller.FullName : "Người bán",
            likes.GetValueOrDefault(x.Id), names.TryGetValue(x.SellerId, out var user) && user.HasAvatar)).ToList();
    }
    public void AddFavorite(Favorite favorite) => db.Favorites.Add(favorite);
    public void RemoveFavorite(string userId, Guid productId) =>
        db.Favorites.Remove(new Favorite { UserId = userId, ProductId = productId });
    public void AddFollow(SellerFollow follow) => db.SellerFollows.Add(follow);
    public void RemoveFollow(string followerId, string sellerId) =>
        db.SellerFollows.Remove(new SellerFollow { FollowerId = followerId, SellerId = sellerId });
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
