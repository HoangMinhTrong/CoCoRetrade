using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoCoReTrade.Tests;

public sealed class SystemServicesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProductCodesUseOneAtomicSequenceAndDistinctPrefixes()
    {
        var sequence = new Sequence();
        var codes = new ProductCodeService(sequence, new Clock(Start));
        var all = await Task.WhenAll(Enumerable.Range(0, 80).Select(i =>
            i % 2 == 0 ? codes.NewConsignmentCodeAsync(default) : codes.NewInventorySkuAsync(default)));
        Assert.Equal(80, all.Distinct().Count());
        Assert.Equal(40, all.Count(x => x.StartsWith("CG-2026-")));
        Assert.Equal(40, all.Count(x => x.StartsWith("CC-2026-")));
    }

    [Fact]
    public async Task ShippingZonesComeFromRepositoryAndCommissionExcludesShipping()
    {
        var shipping = new ShippingFeeService(new Zones());
        Assert.Equal(0, await shipping.FeeAsync(FulfillmentType.Pickup, null, null, default));
        Assert.Equal(20_000, await shipping.FeeAsync(FulfillmentType.Shipping, "Hồ Chí Minh", "Bến Nghé", default));
        Assert.Equal(35_000, await shipping.FeeAsync(FulfillmentType.Shipping, "Hà Nội", "Ba Đình", default));
        var commission = new CommissionService(new MarketplacePolicy(25, 15));
        Assert.Equal(37_500, commission.Calculate(250_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketplacePolicy(25, 21));
    }

    [Fact]
    public async Task ExpirationRecordsOneEventAndCannotReexpireOrAccept()
    {
        var product = ActiveProduct();
        var offer = Offer.Create(product, "buyer", 240_000, 225_000, 0, Start.UtcDateTime);
        var repository = new Offers(offer, product);
        var clock = new Clock(Start.AddHours(24));
        var service = new OfferExpirationService(repository, clock);
        Assert.Equal(1, await service.ExpireDueAsync(10, default));
        Assert.Equal(OfferStatus.Expired, offer.Status);
        Assert.Single(offer.Events, x => x.Action == OfferEventAction.Expired);
        Assert.Equal(2, repository.Notifications.Count);
        Assert.Equal(0, await service.ExpireDueAsync(10, default));
        Assert.Throws<InvalidOperationException>(() => offer.Accept("seller", product.Status, clock.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public async Task OrderExpirationOnlyCancelsEligibleUnpaidOrders()
    {
        var repository = new DueOrders();
        var orders = new CancellingOrders();
        var service = new OrderExpirationService(repository, orders, new OrderExpirationPolicy(30), new Clock(Start));
        Assert.Equal(1, await service.ExpireUnpaidAsync(10, default));
        Assert.Equal(repository.OldId, Assert.Single(orders.Cancelled));
        Assert.Equal("system", orders.Actor);
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderExpirationPolicy(14));
    }

    [Fact]
    public async Task WithdrawalReservationIsIdempotentAndRejectionRefundsExactlyOnce()
    {
        var repository = new Wallets();
        var service = new WalletService(repository, new UnusedAdminWallets(), new Permissions(), new Clock(Start));
        var key = Guid.NewGuid();
        var request = await service.RequestWithdrawalAsync("seller", key, 60_000, "****1234", default);
        Assert.Equal(40_000, repository.Wallet.Balance);
        var replay = await service.RequestWithdrawalAsync("seller", key, 60_000, "****1234", default);
        Assert.Equal(request.Id, replay.Id);
        Assert.Single(repository.Transactions);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DecideWithdrawalAsync("buyer", request.Id, true, null, default));
        var rejected = await service.DecideWithdrawalAsync("admin", request.Id, false, "Invalid destination", default);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal(100_000, repository.Wallet.Balance);
        Assert.Equal(2, repository.Transactions.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideWithdrawalAsync("admin", request.Id, false, "again", default));
    }

    [Fact]
    public async Task ApprovalDoesNotClaimBankTransferCompleted()
    {
        var repository = new Wallets();
        var service = new WalletService(repository, new UnusedAdminWallets(), new Permissions(), new Clock(Start));
        var request = await service.RequestWithdrawalAsync("seller", Guid.NewGuid(), 50_000, "****4321", default);
        var approved = await service.DecideWithdrawalAsync("admin", request.Id, true, null, default);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(50_000, repository.Wallet.Balance);
        Assert.Single(repository.Transactions);
        Assert.Equal(0, (await service.BalanceAsync("seller", default)).Withdrawn);
    }

    [Fact]
    public async Task OutboxMarksOnlyProviderDeliveredMessages()
    {
        var repository = new Outbox();
        var provider = new EmailProvider();
        var dispatcher = new OutboxDispatcher(repository, [provider], [], new Clock(Start),
            NullLogger<OutboxDispatcher>.Instance);
        Assert.Equal(1, await dispatcher.DispatchAsync(5, default));
        Assert.Equal(repository.Message.Id, Assert.Single(provider.Sent));
        Assert.True(repository.MarkedSent);

        var unavailable = new Outbox("webpush");
        var withoutPush = new OutboxDispatcher(unavailable, [], [], new Clock(Start),
            NullLogger<OutboxDispatcher>.Instance);
        Assert.Equal(0, await withoutPush.DispatchAsync(5, default));
        Assert.False(unavailable.MarkedSent);
        Assert.True(unavailable.Deferred);
    }

    private static Product ActiveProduct()
    {
        var product = Product.Submit("seller", "CG-2026-000100", "Linen shirt", "Zara", "Shirts", "M",
            500_000, 300_000, 95, true, [new("a", "image/jpeg", 0), new("b", "image/jpeg", 1), new("c", "image/jpeg", 2)]);
        product.RecordInspection("inspector", true, product.Name, product.Brand, product.Category, product.Size,
            product.OriginalPrice, product.AskingPrice, 95, "Physical item checked", "CC-2026-000100", [], Start.UtcDateTime);
        product.Publish(Start.UtcDateTime);
        return product;
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Sequence : IProductCodeSequence
    {
        private long value;
        public Task<long> NextAsync(CancellationToken ct) => Task.FromResult(Interlocked.Increment(ref value));
    }
    private sealed class Zones : IShippingZoneRepository
    {
        public Task<bool> IsInnerCityAsync(string province, string ward, CancellationToken ct) =>
            Task.FromResult(province == "Hồ Chí Minh" && ward == "Bến Nghé");
    }
    private sealed class Offers(Offer offer, Product product) : IOfferRepository
    {
        public List<MarketplaceNotification> Notifications { get; } = [];
        public Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) => work();
        public Task<Product?> ProductAsync(Guid id, CancellationToken ct) => Task.FromResult<Product?>(product);
        public Task<Offer?> OfferAsync(Guid id, CancellationToken ct) => Task.FromResult<Offer?>(id == offer.Id ? offer : null);
        public Task<IReadOnlyList<Offer>> ForUserAsync(string userId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Offer>>([offer]);
        public Task<int> BuyerPriceCountAsync(Guid productId, string buyerId, CancellationToken ct) => Task.FromResult(1);
        public Task<IReadOnlyList<Guid>> DueForExpirationAsync(DateTime now, int batch, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(offer.Status != OfferStatus.Expired && offer.ExpiresAtUtc <= now ? [offer.Id] : []);
        public Task AddOfferAsync(Offer item, CancellationToken ct) => Task.CompletedTask;
        public void AddEvent(OfferEvent item) { }
        public void AddNotification(MarketplaceNotification item) => Notifications.Add(item);
        public Task<IReadOnlyList<MarketplaceNotification>> NotificationsAsync(string userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MarketplaceNotification>>(Notifications);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class DueOrders : IOrderRepository
    {
        public Guid OldId { get; } = Guid.NewGuid();
        public Task<IReadOnlyList<Guid>> DueUnpaidOrdersAsync(DateTime cutoff, int batch, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>([OldId]);
        public Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) => work();
        public Task<Product?> ProductAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Offer?> OfferAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Order?> OrderAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<Order?> ByIdempotencyAsync(string buyerId, Guid key, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Order>> MineAsync(string userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Order>> AdminQueueAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(Order order, CancellationToken ct) => throw new NotImplementedException();
        public void AddOfferEvent(OfferEvent item) => throw new NotImplementedException();
        public void AddNotification(MarketplaceNotification item) => throw new NotImplementedException();
        public void AddAudit(AuditLog item) => throw new NotImplementedException();
        public Task SaveAsync(CancellationToken ct) => throw new NotImplementedException();
    }
    private sealed class CancellingOrders : IOrderService
    {
        public List<Guid> Cancelled { get; } = [];
        public string? Actor { get; private set; }
        public Task<OrderDto> CancelAsync(Guid id, string actorId, bool isAdmin, CancellationToken ct)
        { Cancelled.Add(id); Actor = actorId; return Task.FromResult<OrderDto>(null!); }
        public Task<CheckoutQuote> QuoteAsync(Guid id, Guid? offer, string buyer, FulfillmentType type, string? province, string? ward, CancellationToken ct) => throw new NotImplementedException();
        public Task<OrderDto> PlaceAsync(CheckoutRequest request, string buyer, CancellationToken ct) => throw new NotImplementedException();
        public Task<OrderDto?> DetailsAsync(Guid id, string viewer, bool admin, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<OrderDto>> MineAsync(string user, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<OrderDto>> AdminQueueAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<OrderDto> MarkReadyAsync(Guid id, string admin, CancellationToken ct) => throw new NotImplementedException();
        public Task<OrderDto> ConfirmPaymentAsync(Guid id, string admin, string reference, CancellationToken ct) => throw new NotImplementedException();
        public Task<OrderDto> CompleteAsync(Guid id, string admin, CancellationToken ct) => throw new NotImplementedException();
    }
    private sealed class Permissions : IStaffPermissionChecker
    {
        public Task EnsureAsync(string actor, string permission, CancellationToken ct) =>
            actor == "admin" ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
    }
    private sealed class Wallets : IWalletWithdrawalRepository
    {
        public Wallet Wallet { get; } = new() { Id = Guid.NewGuid(), UserId = "seller", Balance = 100_000 };
        public List<WithdrawalRequest> Requests { get; } = [];
        public List<WalletTransaction> Transactions { get; } = [];
        public Task<IReadOnlyList<WithdrawalRequest>> ForUserWithdrawalsAsync(string userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<WithdrawalRequest>>(Requests);
        public Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) => work();
        public Task<Wallet?> WalletForUserAsync(string user, CancellationToken ct) => Task.FromResult<Wallet?>(user == "seller" ? Wallet : null);
        public Task<Wallet?> WalletByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Wallet?>(id == Wallet.Id ? Wallet : null);
        public Task<WithdrawalRequest?> WithdrawalAsync(Guid id, CancellationToken ct) => Task.FromResult<WithdrawalRequest?>(Requests.FirstOrDefault(x => x.Id == id));
        public Task<WithdrawalRequest?> ByRequestKeyAsync(Guid walletId, Guid key, CancellationToken ct) =>
            Task.FromResult<WithdrawalRequest?>(Requests.FirstOrDefault(x => x.WalletId == walletId && x.RequestKey == key));
        public Task<WalletBalanceDto> BalanceAsync(string user, CancellationToken ct) =>
            Task.FromResult(new WalletBalanceDto(0, Wallet.Balance,
                Requests.Where(x => x.Status is "Pending" or "Approved").Sum(x => x.Amount), 0));
        public Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(["admin"]);
        public void AddWithdrawal(WithdrawalRequest request) => Requests.Add(request);
        public void AddTransaction(WalletTransaction transaction) => Transactions.Add(transaction);
        public void AddAudit(AuditLog audit) { }
        public void AddNotification(MarketplaceNotification notification) { }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class UnusedAdminWallets : IAdminWalletService
    {
        public Task<AdminWalletDetails> CreditCompletedOrderAsync(string actor, Guid order, bool confirmed, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<AdminWalletRow>> ListAsync(string actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<AdminWalletDetails?> DetailsAsync(string actor, Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<AdminWithdrawalRow>> WithdrawalsAsync(string actor, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<SellerPayoutCandidate>> PayoutCandidatesAsync(string actor, CancellationToken ct) => throw new NotImplementedException();
    }
    private sealed class Outbox(string channel = "email") : IOutboxRepository
    {
        public OutboxDelivery Message { get; } = new(Guid.NewGuid(), Guid.NewGuid(), channel,
            "seller", "seller@example.test", "Orders", "Ready", 1);
        public bool MarkedSent { get; private set; }
        public bool Deferred { get; private set; }
        private bool claimed;
        public Task<OutboxDelivery?> ClaimNextAsync(DateTime now, CancellationToken ct)
        { if (claimed) return Task.FromResult<OutboxDelivery?>(null); claimed = true; return Task.FromResult<OutboxDelivery?>(Message); }
        public Task MarkSentAsync(Guid id, Guid lease, DateTime now, CancellationToken ct)
        { MarkedSent = true; return Task.CompletedTask; }
        public Task DeferAsync(Guid id, Guid lease, DateTime next, string? error, CancellationToken ct)
        { Deferred = true; return Task.CompletedTask; }
    }
    private sealed class EmailProvider : IEmailNotificationProvider
    {
        public List<Guid> Sent { get; } = [];
        public Task SendAsync(Guid id, string email, string category, string message, CancellationToken ct)
        { Sent.Add(id); return Task.CompletedTask; }
    }
}
