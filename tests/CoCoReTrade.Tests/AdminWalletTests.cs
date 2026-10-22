using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Tests;

public sealed class AdminWalletTests
{
    private static (Order Order, Product Product) MakeOrder(bool completed)
    {
        var now = DateTime.UtcNow;
        var product = Product.Submit("seller", "CG-2026-000100", "Linen shirt", "Zara", "Shirts", "M",
            500_000, 300_000, 95, true,
            [new ProductImage("a.jpg", "image/jpeg", 0), new ProductImage("b.jpg", "image/jpeg", 1), new ProductImage("c.jpg", "image/jpeg", 2)]);
        product.ReviewPreliminarily("admin", PreliminaryReviewStatus.Approved, null, now);
        product.RecordInspection("inspector", true, product.Name, product.Brand, product.Category,
            product.Size, product.OriginalPrice, product.AskingPrice, 95, "Physical item checked", [], now);
        product.Publish("CC-2026-000100", now);
        var order = Order.Create(product, null, "buyer", Guid.NewGuid(), FulfillmentType.Shipping,
            "Buyer Name", "0905123456", "Hanoi", "Ward A", "12 Main Street", null, null,
            20_000, 15, now);
        product.Reserve();
        if (completed)
        {
            order.MarkReady();
            order.ConfirmPayment("admin", "CASH-1234", now);
            order.Complete(product, now);
        }
        return (order, product);
    }

    [Fact]
    public async Task UnpaidOrUncompletedOrderCannotCreditWallet()
    {
        var (order, _) = MakeOrder(false);
        var repo = new MemoryWallets(order);
        var service = new AdminWalletService(repo, new AdminOnly(), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreditCompletedOrderAsync("admin", order.Id, true, default));
        Assert.Empty(repo.Transactions);
        Assert.Null(repo.Wallet);
    }

    [Fact]
    public async Task CompletedPaidOrderCreditsMerchandiseMinusCommissionOnce()
    {
        var (order, _) = MakeOrder(true);
        var repo = new MemoryWallets(order);
        var service = new AdminWalletService(repo, new AdminOnly(), TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreditCompletedOrderAsync("buyer", order.Id, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreditCompletedOrderAsync("admin", order.Id, false, default));
        var result = await service.CreditCompletedOrderAsync("admin", order.Id, true, default);
        Assert.Equal(300_000, order.ItemAmount);
        Assert.Equal(20_000, order.ShippingFee);
        Assert.Equal(45_000, order.CommissionAmount);
        Assert.Equal(255_000, result.Wallet.Balance);
        Assert.Equal(255_000, Assert.Single(repo.Transactions).Amount);
        Assert.Single(repo.Audits);
        Assert.Single(repo.Notifications);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreditCompletedOrderAsync("admin", order.Id, true, default));
        Assert.Equal(255_000, repo.Wallet!.Balance);
    }

    private sealed class AdminOnly : IStaffPermissionChecker
    {
        public Task EnsureAsync(string actorId, string permission, CancellationToken ct) =>
            actorId == "admin" && permission == StaffPermissions.ManageWallets
                ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
    }
    private sealed class MemoryWallets(Order order) : IAdminWalletRepository
    {
        public Wallet? Wallet { get; private set; }
        public List<WalletTransaction> Transactions { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public List<MarketplaceNotification> Notifications { get; } = [];
        public Task<IReadOnlyList<AdminWalletRow>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdminWalletRow>>([]);
        public Task<AdminWalletDetails?> DetailsAsync(Guid id, CancellationToken ct)
        {
            AdminWalletDetails? result = Wallet?.Id == id ? new AdminWalletDetails(
                new AdminWalletRow(id, Wallet.UserId, "Seller", "seller@example.com", Wallet.Balance, Transactions.Count),
                Transactions.Select(x => new AdminWalletTransaction(x.Id, x.OrderId, x.Amount, x.Type, x.CreatedAtUtc)).ToList(), []) : null;
            return Task.FromResult(result);
        }
        public Task<IReadOnlyList<AdminWithdrawalRow>> WithdrawalsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdminWithdrawalRow>>([]);
        public Task<IReadOnlyList<SellerPayoutCandidate>> PayoutCandidatesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SellerPayoutCandidate>>([]);
        public Task<Order?> OrderAsync(Guid id, CancellationToken ct) => Task.FromResult<Order?>(order.Id == id ? order : null);
        public Task<Wallet?> WalletForUserAsync(string userId, CancellationToken ct) => Task.FromResult(Wallet?.UserId == userId ? Wallet : null);
        public Task<bool> HasSellerPayoutAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult(Transactions.Any(x => x.OrderId == orderId && x.Type == "SellerPayout"));
        public async Task<Guid> InTransactionAsync(Func<Task<Guid>> work, CancellationToken ct) => await work();
        public void AddWallet(Wallet wallet) => Wallet = wallet;
        public void AddTransaction(WalletTransaction transaction) => Transactions.Add(transaction);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(MarketplaceNotification notification) => Notifications.Add(notification);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
