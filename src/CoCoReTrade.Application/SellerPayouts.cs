using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

// Called inside the caller's database transaction. The order, wallet and ledger commit together.
public interface ISellerPayoutService
{
    Task<Guid> CreditCompletedOrderAsync(Order order, string actorId, DateTime nowUtc, CancellationToken ct);
}

public sealed class SellerPayoutService(IAdminWalletRepository repository,
    IPlatformCommissionRepository? platformCommissions = null) : ISellerPayoutService
{
    public async Task<Guid> CreditCompletedOrderAsync(Order order, string actorId, DateTime nowUtc, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Completed || order.PaymentStatus != PaymentStatus.Paid)
            throw new InvalidOperationException("Chỉ đơn đã hoàn tất và thanh toán mới được ghi có ví người bán.");
        if (await repository.HasSellerPayoutAsync(order.Id, ct))
            throw new InvalidOperationException("Tiền bán hàng của đơn này đã được ghi có.");
        var commissionAlreadyRecorded = platformCommissions is not null &&
            await platformCommissions.HasEntryAsync(order.Id, ct);

        var proceeds = order.ItemAmount - order.CommissionAmount;
        if (proceeds <= 0) throw new InvalidOperationException("Đơn hàng không có số tiền hợp lệ để ghi có ví người bán.");
        var wallet = await repository.WalletForUserAsync(order.SellerId, ct);
        if (wallet is null)
        {
            wallet = new Wallet { Id = Guid.NewGuid(), UserId = order.SellerId };
            repository.AddWallet(wallet);
        }
        wallet.Credit(proceeds);
        repository.AddTransaction(new WalletTransaction
        {
            Id = Guid.NewGuid(), WalletId = wallet.Id, OrderId = order.Id,
            Amount = proceeds, Type = "SellerPayout", CreatedAtUtc = nowUtc
        });
        // Both receipts are saved by the caller in the same SQL transaction.
        if (!commissionAlreadyRecorded)
            platformCommissions?.Add(new PlatformCommissionEntry(order, nowUtc));
        repository.AddAudit(new AuditLog
        {
            Id = Guid.NewGuid(), ActorId = actorId, EntityType = "Wallet",
            EntityId = wallet.Id.ToString(), Action = "Wallet.SellerPayout",
            Details = $"Order={order.Id}; merchandise={order.ItemAmount}; commission={order.CommissionAmount}; credited={proceeds}",
            CreatedAtUtc = nowUtc
        });
        repository.AddNotification(new MarketplaceNotification
        {
            Id = Guid.NewGuid(), UserId = order.SellerId, OrderId = order.Id,
            Category = "Orders", Message = $"CoCo đã ghi có {proceeds:N0} VNĐ vào ví từ đơn {order.ProductName}.",
            CreatedAtUtc = nowUtc
        });
        return wallet.Id;
    }
}
