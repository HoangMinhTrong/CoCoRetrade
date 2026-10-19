using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AdminHeldPickupRepository(AppDbContext db) : IAdminHeldPickupRepository
{
    public async Task<IReadOnlyList<HeldPickupOrderDto>> ListAsync(CancellationToken ct) =>
        await db.Orders.AsNoTracking()
            .Join(db.Products.AsNoTracking(), order => order.ProductId, product => product.Id,
                (order, product) => new { order, product })
            .Where(x => x.product.Status == ProductStatus.Reserved &&
                x.order.Fulfillment == FulfillmentType.Pickup &&
                (x.order.Status == OrderStatus.PendingProcessing || x.order.Status == OrderStatus.ReadyForPickup))
            .OrderBy(x => x.order.PickupAtUtc)
            .Select(x => new HeldPickupOrderDto(x.order.Id, x.order.ProductId,
                x.order.ProductSku, x.order.ProductName, x.order.RecipientName,
                x.order.RecipientPhone, x.order.PickupAtUtc,
                x.order.PickupDeadlineAtUtc, x.order.Status,
                x.order.OfferId != null, x.order.TotalAmount))
            .Take(300).ToListAsync(ct);
}
