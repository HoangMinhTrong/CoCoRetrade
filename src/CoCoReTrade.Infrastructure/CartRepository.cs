using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class CartRepository(AppDbContext db) : ICartRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }
    public Task<Product?> ProductAsync(Guid productId, CancellationToken ct) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, ct);
    public Task<bool> ContainsAsync(string userId, Guid productId, CancellationToken ct) =>
        db.CartItems.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ProductId == productId, ct);
    public Task<int> CountAsync(string userId, CancellationToken ct) =>
        db.CartItems.CountAsync(x => x.UserId == userId, ct);
    public Task PruneUnavailableAsync(string userId, CancellationToken ct) =>
        db.CartItems.Where(x => x.UserId == userId &&
                !db.Products.Any(p => p.Id == x.ProductId && p.Status == ProductStatus.Active))
            .ExecuteDeleteAsync(ct);
    public async Task<IReadOnlyList<Guid>> ProductIdsAsync(string userId, CancellationToken ct) =>
        await db.CartItems.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => x.ProductId).Take(20).ToListAsync(ct);
    public void Add(CartItem item) => db.CartItems.Add(item);
    public void Remove(string userId, Guid productId) =>
        db.CartItems.Remove(new CartItem { UserId = userId, ProductId = productId });
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
