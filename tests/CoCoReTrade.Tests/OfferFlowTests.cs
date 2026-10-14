using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Tests;

public sealed class OfferFlowTests
{
    private const string Buyer = "buyer-1";
    private const string Seller = "seller-1";
    private const string Outsider = "outsider-1";
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static (Product Product, OfferService Service, MemoryStore Store, ManualClock Clock) Setup(bool active = true, bool allowOffers = true)
    {
        var product = Product.Submit(Seller, "CC-2026-000100", "Áo khoác denim", "Zara", "Áo", "M",
            500_000, 300_000, 95, allowOffers,
            [new ProductImage("a.jpg", "image/jpeg", 0), new ProductImage("b.jpg", "image/jpeg", 1), new ProductImage("c.jpg", "image/jpeg", 2)]);
        if (active) { product.RecordInspection("inspector-1", true, product.Name, product.Brand, product.Category, product.Size, product.OriginalPrice, product.AskingPrice, 95, "Physical item checked", "CC-2026-000100", [], Start.UtcDateTime); product.Publish(Start.UtcDateTime); }
        var store = new MemoryStore(product);
        var clock = new ManualClock(Start);
        var policy = new MarketplacePolicy(25, 15);
        return (product, new OfferService(store, policy, clock), store, clock);
    }

    [Fact]
    public async Task OnlyOtherBuyersCanOfferOnActiveOfferableProduct()
    {
        var (activeProduct, service, _, _) = Setup();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.MakeAsync(activeProduct.Id, Seller, 240_000, default));
        var (pending, pendingService, _, _) = Setup(active: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pendingService.MakeAsync(pending.Id, Buyer, 240_000, default));
        var (disabled, disabledService, _, _) = Setup(allowOffers: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => disabledService.MakeAsync(disabled.Id, Buyer, 240_000, default));
    }

    [Fact]
    public async Task DiscountIsConfigurableWithinTwentyToThirtyPercent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketplacePolicy(19));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketplacePolicy(31));
        var (product, service, _, _) = Setup();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.MakeAsync(product.Id, Buyer, 224_999, default));
        var accepted = await service.MakeAsync(product.Id, Buyer, 225_000, default);
        Assert.Equal(225_000, accepted.CurrentPrice);
    }

    [Fact]
    public async Task ThreeBuyerPricesAcrossSessionsAndCountersAreTheLimit()
    {
        var (product, service, _, _) = Setup();
        var offer = await service.MakeAsync(product.Id, Buyer, 230_000, default);
        await service.RespondAsync(offer.Id, Seller, OfferResponse.Counter, 280_000, default);
        await service.RespondAsync(offer.Id, Buyer, OfferResponse.Counter, 240_000, default);
        await service.RespondAsync(offer.Id, Seller, OfferResponse.Counter, 270_000, default);
        var last = await service.RespondAsync(offer.Id, Buyer, OfferResponse.Counter, 250_000, default);
        Assert.Equal(3, last.BuyerOffersUsed);
        await service.RespondAsync(offer.Id, Seller, OfferResponse.Counter, 260_000, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RespondAsync(offer.Id, Buyer, OfferResponse.Counter, 255_000, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MakeAsync(product.Id, Buyer, 260_000, default));
    }

    [Fact]
    public async Task OnlyReceivingPartyCanRespondAndOnlyBuyerCanUseAcceptedPrice()
    {
        var (product, service, _, _) = Setup();
        var offer = await service.MakeAsync(product.Id, Buyer, 230_000, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RespondAsync(offer.Id, Outsider, OfferResponse.Accept, null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RespondAsync(offer.Id, Buyer, OfferResponse.Accept, null, default));
        Assert.Null(await service.DetailsAsync(offer.Id, Outsider, default));
        await service.RespondAsync(offer.Id, Seller, OfferResponse.Accept, null, default);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task PendingAndAcceptedPricesExpireAfterTwentyFourHours()
    {
        var (product, service, _, clock) = Setup();
        var offer = await service.MakeAsync(product.Id, Buyer, 230_000, default);
        clock.Advance(TimeSpan.FromHours(24));
        Assert.Equal(OfferStatus.Expired, (await service.DetailsAsync(offer.Id, Buyer, default))!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RespondAsync(offer.Id, Seller, OfferResponse.Accept, null, default));

        var second = await service.MakeAsync(product.Id, Buyer, 240_000, default);
        await service.RespondAsync(second.Id, Seller, OfferResponse.Accept, null, default);
        clock.Advance(TimeSpan.FromHours(24));
        Assert.Equal(OfferStatus.Expired, (await service.DetailsAsync(second.Id, Buyer, default))!.Status);
    }

    [Fact]
    public async Task CounterAndAcceptanceUseFinalPriceAndReserveOnlyOnOrder()
    {
        var (product, service, store, _) = Setup();
        var offer = await service.MakeAsync(product.Id, Buyer, 230_000, default);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RespondAsync(offer.Id, Seller, OfferResponse.Counter, 220_000, default));
        await service.RespondAsync(offer.Id, Seller, OfferResponse.Counter, 260_000, default);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RespondAsync(offer.Id, Buyer, OfferResponse.Counter, 260_000, default));
        await service.RespondAsync(offer.Id, Buyer, OfferResponse.Counter, 245_000, default);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RespondAsync(offer.Id, Seller, OfferResponse.Counter, 270_000, default));
        var accepted = await service.RespondAsync(offer.Id, Seller, OfferResponse.Accept, null, default);
        Assert.Equal(245_000, accepted.AcceptedPrice);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal(4, accepted.Events.Count);
        Assert.Equal(4, store.Notifications.Count);

        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task BuyerCanAcceptOrDeclineSellerCounter()
    {
        var (product, service, _, _) = Setup();
        var first = await service.MakeAsync(product.Id, Buyer, 230_000, default);
        await service.RespondAsync(first.Id, Seller, OfferResponse.Counter, 250_000, default);
        var accepted = await service.RespondAsync(first.Id, Buyer, OfferResponse.Accept, null, default);
        Assert.Equal(250_000, accepted.AcceptedPrice);

        var second = await service.MakeAsync(product.Id, Buyer, 240_000, default);
        await service.RespondAsync(second.Id, Seller, OfferResponse.Counter, 270_000, default);
        var declined = await service.RespondAsync(second.Id, Buyer, OfferResponse.Decline, null, default);
        Assert.Equal(OfferStatus.Declined, declined.Status);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now = _now.Add(value);
    }
    private sealed class MemoryStore(Product product) : IOfferRepository
    {
        public List<Offer> Offers { get; } = [];
        public List<MarketplaceNotification> Notifications { get; } = [];
        public async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct) => await work();
        public Task<Product?> ProductAsync(Guid id, CancellationToken ct) => Task.FromResult<Product?>(id == product.Id ? product : null);
        public Task<Offer?> OfferAsync(Guid id, CancellationToken ct) => Task.FromResult<Offer?>(Offers.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<Offer>> ForUserAsync(string userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Offer>>(Offers.Where(x => x.BuyerId == userId || x.SellerId == userId).ToList());
        public Task<int> BuyerPriceCountAsync(Guid productId, string buyerId, CancellationToken ct) =>
            Task.FromResult(Offers.Where(x => x.ProductId == productId && x.BuyerId == buyerId)
                .Sum(x => x.Events.Count(e => e.Action is OfferEventAction.Submitted or OfferEventAction.BuyerCountered)));
        public Task<IReadOnlyList<Guid>> DueForExpirationAsync(DateTime nowUtc, int batchSize, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(Offers.Where(x => x.EffectiveStatus(nowUtc) == OfferStatus.Expired &&
                x.Status != OfferStatus.Expired).Take(batchSize).Select(x => x.Id).ToList());
        public Task AddOfferAsync(Offer offer, CancellationToken ct) { Offers.Add(offer); return Task.CompletedTask; }
        public void AddEvent(OfferEvent offerEvent) { }
        public void AddNotification(MarketplaceNotification notification) => Notifications.Add(notification);
        public Task<IReadOnlyList<MarketplaceNotification>> NotificationsAsync(string userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MarketplaceNotification>>(Notifications.Where(x => x.UserId == userId).ToList());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
