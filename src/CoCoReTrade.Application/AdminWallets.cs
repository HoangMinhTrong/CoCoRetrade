using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record AdminWalletRow(Guid Id, string UserId, string OwnerName, string OwnerEmail,
    decimal Balance, int TransactionCount);
public sealed record AdminWalletTransaction(Guid Id, Guid? OrderId, decimal Amount, string Type, DateTime CreatedAtUtc);
public sealed record AdminWithdrawalRow(Guid Id, Guid WalletId, string OwnerName, decimal Amount,
    string BankAccountMasked, string Status);
public sealed record AdminWalletDetails(AdminWalletRow Wallet, IReadOnlyList<AdminWalletTransaction> Transactions,
    IReadOnlyList<AdminWithdrawalRow> Withdrawals);
public sealed record SellerPayoutCandidate(Guid OrderId, string ProductSku, string SellerName,
    decimal MerchandiseAmount, decimal CommissionAmount, decimal NetAmount);

public interface IAdminWalletRepository
{
    Task<IReadOnlyList<AdminWalletRow>> ListAsync(CancellationToken ct);
    Task<AdminWalletDetails?> DetailsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdminWithdrawalRow>> WithdrawalsAsync(CancellationToken ct);
    Task<IReadOnlyList<SellerPayoutCandidate>> PayoutCandidatesAsync(CancellationToken ct);
    Task<Order?> OrderAsync(Guid id, CancellationToken ct);
    Task<Wallet?> WalletForUserAsync(string userId, CancellationToken ct);
    Task<bool> HasSellerPayoutAsync(Guid orderId, CancellationToken ct);
    Task<Guid> InTransactionAsync(Func<Task<Guid>> work, CancellationToken ct);
    void AddWallet(Wallet wallet);
    void AddTransaction(WalletTransaction transaction);
    void AddAudit(AuditLog audit);
    void AddNotification(MarketplaceNotification notification);
    Task SaveAsync(CancellationToken ct);
}
public interface IAdminWalletService
{
    Task<IReadOnlyList<AdminWalletRow>> ListAsync(string actorId, CancellationToken ct);
    Task<AdminWalletDetails?> DetailsAsync(string actorId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdminWithdrawalRow>> WithdrawalsAsync(string actorId, CancellationToken ct);
    Task<IReadOnlyList<SellerPayoutCandidate>> PayoutCandidatesAsync(string actorId, CancellationToken ct);
    Task<AdminWalletDetails> CreditCompletedOrderAsync(string actorId, Guid orderId, bool confirmed, CancellationToken ct);
}
public sealed class AdminWalletService(IAdminWalletRepository repository, IStaffPermissionChecker permissions,
    TimeProvider clock) : IAdminWalletService
{
    public async Task<IReadOnlyList<AdminWalletRow>> ListAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        return await repository.ListAsync(ct);
    }
    public async Task<AdminWalletDetails?> DetailsAsync(string actorId, Guid id, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        return await repository.DetailsAsync(id, ct);
    }
    public async Task<IReadOnlyList<AdminWithdrawalRow>> WithdrawalsAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        return await repository.WithdrawalsAsync(ct);
    }
    public async Task<IReadOnlyList<SellerPayoutCandidate>> PayoutCandidatesAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        return await repository.PayoutCandidatesAsync(ct);
    }
    public async Task<AdminWalletDetails> CreditCompletedOrderAsync(string actorId, Guid orderId, bool confirmed, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        if (!confirmed || orderId == Guid.Empty) throw new ArgumentException("Confirm the completed and paid order before releasing seller proceeds.");
        var walletId = await repository.InTransactionAsync(async () =>
        {
            var order = await repository.OrderAsync(orderId, ct) ?? throw new KeyNotFoundException("Order not found.");
            if (order.Status != OrderStatus.Completed || order.PaymentStatus != PaymentStatus.Paid)
                throw new InvalidOperationException("Only completed and paid orders can credit the seller wallet.");
            if (await repository.HasSellerPayoutAsync(orderId, ct))
                throw new InvalidOperationException("Seller proceeds were already released for this order.");
            var proceeds = order.ItemAmount - order.CommissionAmount;
            if (proceeds <= 0) throw new InvalidOperationException("The order has no positive seller proceeds.");
            var wallet = await repository.WalletForUserAsync(order.SellerId, ct);
            if (wallet is null)
            {
                wallet = new Wallet { Id = Guid.NewGuid(), UserId = order.SellerId };
                repository.AddWallet(wallet);
            }
            wallet.Credit(proceeds);
            var now = clock.GetUtcNow().UtcDateTime;
            repository.AddTransaction(new WalletTransaction { Id = Guid.NewGuid(), WalletId = wallet.Id,
                OrderId = order.Id, Amount = proceeds, Type = "SellerPayout", CreatedAtUtc = now });
            repository.AddAudit(new AuditLog { Id = Guid.NewGuid(), ActorId = actorId,
                EntityType = "Wallet", EntityId = wallet.Id.ToString(), Action = "Wallet.SellerPayout",
                Details = $"Order={order.Id}; merchandise={order.ItemAmount}; commission={order.CommissionAmount}; credited={proceeds}",
                CreatedAtUtc = now });
            repository.AddNotification(new MarketplaceNotification { Id = Guid.NewGuid(), UserId = order.SellerId,
                OrderId = order.Id, Category = "Orders",
                Message = $"CoCo đã ghi có {proceeds:N0} đ vào ví từ đơn {order.ProductSku}.", CreatedAtUtc = now });
            await repository.SaveAsync(ct);
            return wallet.Id;
        }, ct);
        return await repository.DetailsAsync(walletId, ct) ?? throw new InvalidOperationException("Wallet was not found after credit.");
    }
}
