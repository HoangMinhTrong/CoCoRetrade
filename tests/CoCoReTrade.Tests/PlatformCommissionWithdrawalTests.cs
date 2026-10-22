using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Tests;

public sealed class PlatformCommissionWithdrawalTests
{
    [Fact]
    public async Task WithdrawalUsesSavedBankAndCannotSpendMoreThanAvailable()
    {
        var repository = new CommissionStore(17_850);
        var service = new PlatformCommissionService(repository, new AdminPermissions(), TimeProvider.System);
        var first = await service.WithdrawAsync("admin", Guid.NewGuid(), 10_000,
            "1234567890", "Vietcombank", default);
        Assert.Equal(7_850, await repository.AvailableAsync(default));
        Assert.Equal("1234567890", (await service.GetAsync("admin", default)).BankAccountNumber);

        var key = Guid.NewGuid();
        var second = await service.WithdrawAsync("admin", key, 7_000, null, null, default);
        var repeated = await service.WithdrawAsync("admin", key, 7_000, null, null, default);
        Assert.Equal(second.Id, repeated.Id);
        Assert.Equal(first.BankAccountNumber, second.BankAccountNumber);
        Assert.Equal(850, await repository.AvailableAsync(default));
        Assert.Equal(2, repository.Withdrawals.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.WithdrawAsync("admin", Guid.NewGuid(), 851, null, null, default));
    }

    [Fact]
    public async Task WithdrawalRequiresAdminAndBankDetails()
    {
        var service = new PlatformCommissionService(new CommissionStore(20_000), new AdminPermissions(), TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.WithdrawAsync("seller", Guid.NewGuid(), 1_000, "1234567890", "Bank", default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.WithdrawAsync("admin", Guid.NewGuid(), 1_000, null, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.WithdrawAsync("admin", Guid.NewGuid(), 1_000, "123", "Bank", default));
    }

    [Fact]
    public async Task ConcurrentWithdrawalsCannotOverdrawSharedCommissionBalance()
    {
        var repository = new CommissionStore(20_000);
        var service = new PlatformCommissionService(repository, new AdminPermissions(), TimeProvider.System);
        async Task<bool> TryWithdrawAsync()
        {
            try
            {
                await service.WithdrawAsync("admin", Guid.NewGuid(), 15_000,
                    "1234567890", "Vietcombank", default);
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(TryWithdrawAsync(), TryWithdrawAsync());
        Assert.Single(results, success => success);
        Assert.Equal(5_000, await repository.AvailableAsync(default));
    }

    private sealed class AdminPermissions : IStaffPermissionChecker
    {
        public Task EnsureAsync(string actorId, string permission, CancellationToken ct) =>
            actorId == "admin" && permission == StaffPermissions.ManageWallets
                ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
    }

    private sealed class CommissionStore(decimal total) : IPlatformCommissionRepository
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        public List<PlatformCommissionWithdrawal> Withdrawals { get; } = [];
        private AdminPayoutAccount? account;

        public Task<bool> HasEntryAsync(Guid orderId, CancellationToken ct) => Task.FromResult(false);
        public void Add(PlatformCommissionEntry entry) { }
        public Task<bool> IsAdminAsync(string userId, CancellationToken ct) => Task.FromResult(userId == "admin");
        public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
        {
            await gate.WaitAsync(ct);
            try { return await work(); }
            finally { gate.Release(); }
        }
        public Task<PlatformCommissionWithdrawal?> ByRequestKeyAsync(string adminId, Guid requestKey, CancellationToken ct) =>
            Task.FromResult(Withdrawals.FirstOrDefault(x => x.AdminId == adminId && x.RequestKey == requestKey));
        public Task<AdminPayoutAccount?> PayoutAccountAsync(string adminId, CancellationToken ct) =>
            Task.FromResult(account?.AdminId == adminId ? account : null);
        public Task<decimal> AvailableAsync(CancellationToken ct) =>
            Task.FromResult(total - Withdrawals.Sum(x => x.Amount));
        public void AddWithdrawal(PlatformCommissionWithdrawal withdrawal) => Withdrawals.Add(withdrawal);
        public void AddPayoutAccount(AdminPayoutAccount value) => account = value;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<PlatformCommissionWallet> SnapshotAsync(string adminId, CancellationToken ct) =>
            Task.FromResult(new PlatformCommissionWallet(total - Withdrawals.Sum(x => x.Amount), 1, [],
                total, Withdrawals.Sum(x => x.Amount), account?.BankAccountNumber, account?.BankName, []));
    }
}
