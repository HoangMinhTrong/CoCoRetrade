using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class OrderRepository(AppDbContext db) : IOrderRepository, IAcceptedPickupExpirationRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException) { throw new OrderConcurrencyException("Đơn hàng đã thay đổi. Hãy tải lại trang và thử lại."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627 or 1205)
        { throw new OrderConcurrencyException("Sản phẩm đã có người đặt hoặc yêu cầu bị gửi lặp."); }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new OrderConcurrencyException("Có xung đột khi đặt hàng. Hãy thử lại với cùng yêu cầu."); }
    }
    public Task<Product?> ProductAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Offer?> OfferAsync(Guid id, CancellationToken ct) =>
        db.Offers.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Order?> OrderAsync(Guid id, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Order?> ByIdempotencyAsync(string buyerId, Guid key, CancellationToken ct) =>
        db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.BuyerId == buyerId && x.IdempotencyKey == key, ct);
    public async Task<IReadOnlyList<Order>> MineAsync(string userId, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(x => x.BuyerId == userId || x.SellerId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
    public async Task<IReadOnlyList<Order>> AdminQueueAsync(CancellationToken ct) =>
        await db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Take(300).ToListAsync(ct);
    public async Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct) =>
        await db.UserRoles.Join(db.Roles, ur => ur.RoleId, role => role.Id,
                (ur, role) => new { ur.UserId, role.Name })
            .Where(x => x.Name == "Admin").Select(x => x.UserId).Distinct().ToListAsync(ct);
    public async Task<IReadOnlyList<Guid>> DueUnpaidOrdersAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.PendingProcessing &&
            x.PaymentStatus == PaymentStatus.Unpaid && x.CreatedAtUtc <= cutoffUtc)
            .OrderBy(x => x.CreatedAtUtc).Select(x => x.Id).Take(batchSize).ToListAsync(ct);
    public async Task<IReadOnlyList<Guid>> DueAcceptedPickupOrdersAsync(DateTime nowUtc, int batchSize, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(x => x.Fulfillment == FulfillmentType.Pickup &&
            x.PickupDeadlineAtUtc != null && x.PickupDeadlineAtUtc <= nowUtc &&
            x.PaymentStatus == PaymentStatus.Unpaid &&
            (x.Status == OrderStatus.PendingProcessing || x.Status == OrderStatus.ReadyForPickup))
            .OrderBy(x => x.PickupDeadlineAtUtc).Select(x => x.Id).Take(batchSize).ToListAsync(ct);
    public async Task AddAsync(Order order, CancellationToken ct) => await db.Orders.AddAsync(order, ct);
    public void AddOfferEvent(OfferEvent offerEvent) => db.OfferEvents.Add(offerEvent);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
