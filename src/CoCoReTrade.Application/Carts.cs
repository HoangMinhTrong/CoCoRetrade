using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public interface ICartRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);
    Task<Product?> ProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<bool> ContainsAsync(string userId, Guid productId, CancellationToken cancellationToken);
    Task<int> CountAsync(string userId, CancellationToken cancellationToken);
    Task PruneUnavailableAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ProductIdsAsync(string userId, CancellationToken cancellationToken);
    void Add(CartItem item);
    void Remove(string userId, Guid productId);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface ICartService
{
    Task AddAsync(string userId, Guid productId, CancellationToken cancellationToken);
    Task RemoveAsync(string userId, Guid productId, CancellationToken cancellationToken);
    Task<int> CountAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductCard>> ItemsAsync(string userId, CancellationToken cancellationToken);
}

public sealed class CartService(ICartRepository repository, IProductService products,
    TimeProvider clock) : ICartService
{
    public Task AddAsync(string userId, Guid productId, CancellationToken ct)
    {
        RequireUser(userId);
        return repository.InTransactionAsync(async () =>
        {
            var product = await repository.ProductAsync(productId, ct);
            if (product is null || product.Status != ProductStatus.Active)
                throw new KeyNotFoundException("Sản phẩm không còn mở bán.");
            if (product.SellerId == userId) throw new InvalidOperationException("Không thể thêm sản phẩm của bạn vào giỏ.");
            if (await repository.ContainsAsync(userId, productId, ct)) return true;
            await repository.PruneUnavailableAsync(userId, ct);
            if (await repository.CountAsync(userId, ct) >= 20)
                throw new InvalidOperationException("Giỏ hàng chỉ chứa tối đa 20 món.");
            repository.Add(new CartItem { UserId = userId, ProductId = productId,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime });
            await repository.SaveAsync(ct);
            return true;
        }, ct);
    }

    public Task RemoveAsync(string userId, Guid productId, CancellationToken ct)
    {
        RequireUser(userId);
        return repository.InTransactionAsync(async () =>
        {
            if (await repository.ContainsAsync(userId, productId, ct))
            {
                repository.Remove(userId, productId);
                await repository.SaveAsync(ct);
            }
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<ProductCard>> ItemsAsync(string userId, CancellationToken ct)
    {
        RequireUser(userId);
        await repository.PruneUnavailableAsync(userId, ct);
        var ids = await repository.ProductIdsAsync(userId, ct);
        return await products.ShopAsync(new ProductFilter(Ids: ids), ct);
    }

    public async Task<int> CountAsync(string userId, CancellationToken ct)
    {
        RequireUser(userId);
        await repository.PruneUnavailableAsync(userId, ct);
        return await repository.CountAsync(userId, ct);
    }

    private static void RequireUser(string userId)
    { if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập."); }
}
