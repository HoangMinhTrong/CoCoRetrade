using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record AdminWalletRow(Guid Id, string UserId, string OwnerName, string OwnerEmail,
    decimal Balance, int TransactionCount);
public sealed record AdminWalletTransaction(Guid Id, Guid? OrderId, decimal Amount, string Type, DateTime CreatedAtUtc,
    Guid? WithdrawalRequestId = null);
public sealed record AdminWithdrawalRow(Guid Id, Guid WalletId, string OwnerName, decimal Amount,
    string BankAccountNumber, string Status, string? BankName = null,
    DateTime? TransferredAtUtc = null, string? TransferReference = null);
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
    TimeProvider clock, ISellerPayoutService? payouts = null) : IAdminWalletService
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
        if (!confirmed || orderId == Guid.Empty) throw new ArgumentException("Hãy xác nhận đơn đã hoàn tất và thanh toán trước khi ghi có ví người bán.");
        var walletId = await repository.InTransactionAsync(async () =>
        {
            var order = await repository.OrderAsync(orderId, ct) ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
            var walletId = await (payouts ?? new SellerPayoutService(repository))
                .CreditCompletedOrderAsync(order, actorId, clock.GetUtcNow().UtcDateTime, ct);
            await repository.SaveAsync(ct);
            return walletId;
        }, ct);
        return await repository.DetailsAsync(walletId, ct) ?? throw new InvalidOperationException("Không tìm thấy ví sau khi ghi có.");
    }
}
