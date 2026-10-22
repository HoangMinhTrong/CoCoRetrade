using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record PlatformCommissionRow(Guid OrderId, string ProductSku, decimal MerchandiseAmount,
    decimal CommissionAmount, DateTime CreatedAtUtc);
public sealed record PlatformWithdrawalRow(Guid Id, decimal Amount, string AdminName,
    string BankName, string BankAccountMasked, DateTime CreatedAtUtc);

public sealed record PlatformCommissionWallet(decimal Balance, int OrderCount,
    IReadOnlyList<PlatformCommissionRow> RecentEntries, decimal TotalCommission,
    decimal TotalWithdrawn, string? BankAccountNumber, string? BankName,
    IReadOnlyList<PlatformWithdrawalRow> RecentWithdrawals);

public interface IPlatformCommissionRepository
{
    Task<bool> HasEntryAsync(Guid orderId, CancellationToken ct);
    void Add(PlatformCommissionEntry entry);
    Task<bool> IsAdminAsync(string userId, CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
    Task<PlatformCommissionWithdrawal?> ByRequestKeyAsync(string adminId, Guid requestKey, CancellationToken ct);
    Task<AdminPayoutAccount?> PayoutAccountAsync(string adminId, CancellationToken ct);
    Task<decimal> AvailableAsync(CancellationToken ct);
    void AddWithdrawal(PlatformCommissionWithdrawal withdrawal);
    void AddPayoutAccount(AdminPayoutAccount account);
    Task SaveAsync(CancellationToken ct);
    Task<PlatformCommissionWallet> SnapshotAsync(string adminId, CancellationToken ct);
}

public interface IPlatformCommissionService
{
    Task<PlatformCommissionWallet> GetAsync(string actorId, CancellationToken ct);
    Task<PlatformCommissionWithdrawal> WithdrawAsync(string actorId, Guid requestKey,
        decimal amount, string? bankAccountNumber, string? bankName, CancellationToken ct);
}

public sealed class PlatformCommissionService(IPlatformCommissionRepository repository,
    IStaffPermissionChecker permissions, TimeProvider clock) : IPlatformCommissionService
{
    public async Task<PlatformCommissionWallet> GetAsync(string actorId, CancellationToken ct)
    {
        await EnsureAdminAsync(actorId, ct);
        return await repository.SnapshotAsync(actorId, ct);
    }

    public async Task<PlatformCommissionWithdrawal> WithdrawAsync(string actorId, Guid requestKey,
        decimal amount, string? bankAccountNumber, string? bankName, CancellationToken ct)
    {
        await EnsureAdminAsync(actorId, ct);
        if (requestKey == Guid.Empty || amount <= 0 || amount != decimal.Truncate(amount))
            throw new ArgumentException("Số tiền rút phải là số VNĐ nguyên lớn hơn 0.");
        var number = bankAccountNumber?.Trim() ?? "";
        var bank = bankName?.Trim() ?? "";
        if (number.Length > 0 || bank.Length > 0)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(number, @"^[0-9]{6,30}$") || bank.Length is < 2 or > 100)
                throw new ArgumentException("Cần nhập số tài khoản từ 6 đến 30 chữ số và tên ngân hàng hợp lệ.");
        }
        return await repository.InTransactionAsync(async () =>
        {
            var existing = await repository.ByRequestKeyAsync(actorId, requestKey, ct);
            if (existing is not null)
            {
                if (existing.Amount != amount || number.Length > 0 &&
                    (existing.BankAccountNumber != number || existing.BankName != bank))
                    throw new InvalidOperationException("Yêu cầu rút này đã được dùng với thông tin khác.");
                return existing;
            }
            var saved = await repository.PayoutAccountAsync(actorId, ct);
            if (number.Length == 0)
            {
                number = saved?.BankAccountNumber ?? "";
                bank = saved?.BankName ?? "";
            }
            if (number.Length == 0 || bank.Length == 0)
                throw new ArgumentException("Hãy nhập số tài khoản và tên ngân hàng trước khi rút.");
            if (amount > await repository.AvailableAsync(ct))
                throw new InvalidOperationException("Số dư hoa hồng khả dụng không đủ.");
            var now = clock.GetUtcNow().UtcDateTime;
            var withdrawal = new PlatformCommissionWithdrawal(requestKey, actorId, amount, number, bank, now);
            repository.AddWithdrawal(withdrawal);
            if (saved is null) repository.AddPayoutAccount(new AdminPayoutAccount(actorId, number, bank, now));
            else if (saved.BankAccountNumber != number || saved.BankName != bank) saved.Update(number, bank, now);
            await repository.SaveAsync(ct);
            return withdrawal;
        }, ct);
    }

    private async Task EnsureAdminAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        if (!await repository.IsAdminAsync(actorId, ct))
            throw new UnauthorizedAccessException("Chỉ quản trị viên được rút hoa hồng CoCo.");
    }
}
