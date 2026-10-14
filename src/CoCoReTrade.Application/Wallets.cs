using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record WalletBalanceDto(decimal PendingSellerProceeds, decimal Available,
    decimal PendingWithdrawal, decimal Withdrawn);
public sealed record WithdrawalDto(Guid Id, decimal Amount, string BankAccountMasked,
    string Status, DateTime CreatedAtUtc, DateTime? DecidedAtUtc, string? DecisionReason);

public interface IWalletWithdrawalRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
    Task<Wallet?> WalletForUserAsync(string userId, CancellationToken ct);
    Task<Wallet?> WalletByIdAsync(Guid id, CancellationToken ct);
    Task<WithdrawalRequest?> WithdrawalAsync(Guid id, CancellationToken ct);
    Task<WithdrawalRequest?> ByRequestKeyAsync(Guid walletId, Guid key, CancellationToken ct);
    Task<WalletBalanceDto> BalanceAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<WithdrawalRequest>> ForUserWithdrawalsAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct);
    void AddWithdrawal(WithdrawalRequest request);
    void AddTransaction(WalletTransaction transaction);
    void AddAudit(AuditLog audit);
    void AddNotification(MarketplaceNotification notification);
    Task SaveAsync(CancellationToken ct);
}

public interface IWalletService
{
    Task<WalletBalanceDto> BalanceAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<WithdrawalDto>> WithdrawalsAsync(string userId, CancellationToken ct);
    Task<WithdrawalDto> RequestWithdrawalAsync(string userId, Guid requestKey,
        decimal amount, string bankAccountMasked, CancellationToken ct);
    Task<WithdrawalDto> DecideWithdrawalAsync(string adminId, Guid requestId,
        bool approve, string? reason, CancellationToken ct);
    Task<AdminWalletDetails> CreditCompletedOrderAsync(string adminId, Guid orderId,
        bool confirmed, CancellationToken ct);
}

public sealed class WalletService(IWalletWithdrawalRepository repository, IAdminWalletService adminWallets,
    IStaffPermissionChecker permissions, TimeProvider clock) : IWalletService
{
    public Task<WalletBalanceDto> BalanceAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Sign in is required.");
        return repository.BalanceAsync(userId, ct);
    }
    public async Task<IReadOnlyList<WithdrawalDto>> WithdrawalsAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Sign in is required.");
        return (await repository.ForUserWithdrawalsAsync(userId, ct)).Select(ToDto).ToList();
    }

    public Task<AdminWalletDetails> CreditCompletedOrderAsync(string adminId, Guid orderId,
        bool confirmed, CancellationToken ct) =>
        adminWallets.CreditCompletedOrderAsync(adminId, orderId, confirmed, ct);

    public Task<WithdrawalDto> RequestWithdrawalAsync(string userId, Guid requestKey,
        decimal amount, string bankAccountMasked, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Sign in is required.");
        if (requestKey == Guid.Empty || amount <= 0 || amount != decimal.Truncate(amount) ||
            string.IsNullOrWhiteSpace(bankAccountMasked) || bankAccountMasked.Length > 60 ||
            !bankAccountMasked.Contains('*'))
            throw new ArgumentException("A valid amount, request key and masked bank account are required.");
        return repository.InTransactionAsync(async () =>
        {
            var wallet = await repository.WalletForUserAsync(userId, ct) ??
                throw new InvalidOperationException("No available wallet balance.");
            var existing = await repository.ByRequestKeyAsync(wallet.Id, requestKey, ct);
            if (existing is not null)
            {
                if (existing.Amount != amount || existing.BankAccountMasked != bankAccountMasked.Trim())
                    throw new InvalidOperationException("Request key was used for different withdrawal details.");
                return ToDto(existing);
            }
            wallet.ReserveWithdrawal(amount);
            var now = clock.GetUtcNow().UtcDateTime;
            var request = new WithdrawalRequest
            {
                Id = Guid.NewGuid(), WalletId = wallet.Id, RequestKey = requestKey,
                Amount = amount, BankAccountMasked = bankAccountMasked.Trim(), CreatedAtUtc = now
            };
            repository.AddWithdrawal(request);
            repository.AddTransaction(new WalletTransaction
            {
                Id = Guid.NewGuid(), WalletId = wallet.Id, WithdrawalRequestId = request.Id,
                Amount = -amount, Type = "WithdrawalReserved", CreatedAtUtc = now
            });
            Audit(userId, wallet.Id, request.Id, "Wallet.WithdrawalRequested", now);
            Notify(userId, $"Yêu cầu rút {amount:N0}đ đã được ghi nhận, đang chờ Admin duyệt.", now);
            foreach (var adminId in await repository.AdminUserIdsAsync(ct))
                if (adminId != userId) Notify(adminId, $"Có yêu cầu rút ví {request.Id} cần xét duyệt.", now);
            await repository.SaveAsync(ct);
            return ToDto(request);
        }, ct);
    }

    public async Task<WithdrawalDto> DecideWithdrawalAsync(string adminId, Guid requestId,
        bool approve, string? reason, CancellationToken ct)
    {
        await permissions.EnsureAsync(adminId, StaffPermissions.ManageWallets, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var request = await repository.WithdrawalAsync(requestId, ct) ??
                throw new KeyNotFoundException("Withdrawal request not found.");
            var wallet = await repository.WalletByIdAsync(request.WalletId, ct) ??
                throw new KeyNotFoundException("Wallet not found.");
            var now = clock.GetUtcNow().UtcDateTime;
            if (approve)
            {
                request.Approve(adminId, now);
                Notify(wallet.UserId, $"Yêu cầu rút {request.Amount:N0}đ đã được duyệt; chưa xác nhận chuyển khoản ngân hàng.", now);
            }
            else
            {
                request.Reject(adminId, reason ?? "", now);
                wallet.Credit(request.Amount);
                repository.AddTransaction(new WalletTransaction
                {
                    Id = Guid.NewGuid(), WalletId = wallet.Id, WithdrawalRequestId = request.Id,
                    Amount = request.Amount, Type = "WithdrawalReleased", CreatedAtUtc = now
                });
                Notify(wallet.UserId, $"Yêu cầu rút {request.Amount:N0}đ bị từ chối; tiền đã trở lại số dư khả dụng.", now);
            }
            Audit(adminId, wallet.Id, request.Id,
                approve ? "Wallet.WithdrawalApproved" : "Wallet.WithdrawalRejected", now);
            await repository.SaveAsync(ct);
            return ToDto(request);
        }, ct);
    }

    private void Audit(string actorId, Guid walletId, Guid requestId, string action, DateTime now) =>
        repository.AddAudit(new AuditLog
        {
            Id = Guid.NewGuid(), ActorId = actorId, EntityType = "Wallet",
            EntityId = walletId.ToString(), Action = action, Details = $"Withdrawal={requestId}", CreatedAtUtc = now
        });
    private void Notify(string userId, string message, DateTime now) =>
        repository.AddNotification(new MarketplaceNotification
        {
            Id = Guid.NewGuid(), UserId = userId, Category = "Wallets", Message = message, CreatedAtUtc = now
        });
    private static WithdrawalDto ToDto(WithdrawalRequest request) => new(request.Id, request.Amount,
        request.BankAccountMasked, request.Status, request.CreatedAtUtc, request.DecidedAtUtc, request.DecisionReason);
}
