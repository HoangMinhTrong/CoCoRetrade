using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public interface ISocialRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
    Task<Product?> ProductAsync(Guid productId, CancellationToken ct);
    Task<bool> SellerExistsAsync(string sellerId, CancellationToken ct);
    Task<string?> SellerNameAsync(string sellerId, CancellationToken ct);
    Task<bool> IsFavoriteAsync(string userId, Guid productId, CancellationToken ct);
    Task<bool> IsFollowingAsync(string followerId, string sellerId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FavoriteIdsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<ProductCard>> SavedProductsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<string>> FollowingIdsAsync(string userId, CancellationToken ct);
    void AddFavorite(Favorite favorite);
    void RemoveFavorite(string userId, Guid productId);
    void AddFollow(SellerFollow follow);
    void RemoveFollow(string followerId, string sellerId);
    void AddNotification(MarketplaceNotification notification);
    Task SaveAsync(CancellationToken ct);
}

public interface ISocialService
{
    Task<bool> SetFavoriteAsync(string userId, Guid productId, bool selected, CancellationToken ct);
    Task<bool> SetFollowAsync(string userId, string sellerId, bool selected, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FavoriteIdsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<ProductCard>> SavedProductsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<string>> FollowingIdsAsync(string userId, CancellationToken ct);
    Task<string?> SellerNameAsync(string sellerId, CancellationToken ct);
}

public sealed class SocialService(ISocialRepository repository, TimeProvider clock) : ISocialService
{
    public Task<bool> SetFavoriteAsync(string userId, Guid productId, bool selected, CancellationToken ct)
    {
        RequireUser(userId);
        return repository.InTransactionAsync(async () =>
        {
            var product = await repository.ProductAsync(productId, ct);
            if (product is null || product.Status != ProductStatus.Active)
                throw new KeyNotFoundException("Sản phẩm không còn mở bán.");
            var existing = await repository.IsFavoriteAsync(userId, productId, ct);
            if (existing == selected) return existing;
            var now = clock.GetUtcNow().UtcDateTime;
            if (selected)
            {
                repository.AddFavorite(new Favorite { UserId = userId, ProductId = productId, CreatedAtUtc = now });
                if (product.SellerId != userId)
                    repository.AddNotification(new MarketplaceNotification { Id = Guid.NewGuid(), UserId = product.SellerId,
                        Category = "Social", ProductId = product.Id,
                        Message = $"Có người thích món đồ {product.Name} của bạn.", CreatedAtUtc = now });
            }
            else repository.RemoveFavorite(userId, productId);
            await repository.SaveAsync(ct);
            return selected;
        }, ct);
    }

    public Task<bool> SetFollowAsync(string userId, string sellerId, bool selected, CancellationToken ct)
    {
        RequireUser(userId);
        if (string.IsNullOrWhiteSpace(sellerId) || sellerId == userId)
            throw new ArgumentException("Không thể theo dõi tài khoản này.");
        return repository.InTransactionAsync(async () =>
        {
            if (!await repository.SellerExistsAsync(sellerId, ct)) throw new KeyNotFoundException("Người bán không tồn tại.");
            var existing = await repository.IsFollowingAsync(userId, sellerId, ct);
            if (existing == selected) return existing;
            var now = clock.GetUtcNow().UtcDateTime;
            if (selected)
            {
                repository.AddFollow(new SellerFollow { FollowerId = userId, SellerId = sellerId, CreatedAtUtc = now });
                repository.AddNotification(new MarketplaceNotification { Id = Guid.NewGuid(), UserId = sellerId,
                    Category = "Social", Message = "Có người bắt đầu theo dõi tủ đồ của bạn.", CreatedAtUtc = now });
            }
            else repository.RemoveFollow(userId, sellerId);
            await repository.SaveAsync(ct);
            return selected;
        }, ct);
    }

    public Task<IReadOnlyList<Guid>> FavoriteIdsAsync(string userId, CancellationToken ct)
    { RequireUser(userId); return repository.FavoriteIdsAsync(userId, ct); }
    public Task<IReadOnlyList<ProductCard>> SavedProductsAsync(string userId, CancellationToken ct)
    { RequireUser(userId); return repository.SavedProductsAsync(userId, ct); }
    public Task<IReadOnlyList<string>> FollowingIdsAsync(string userId, CancellationToken ct)
    { RequireUser(userId); return repository.FollowingIdsAsync(userId, ct); }
    public Task<string?> SellerNameAsync(string sellerId, CancellationToken ct) =>
        repository.SellerNameAsync(sellerId, ct);
    private static void RequireUser(string userId)
    { if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập."); }
}
