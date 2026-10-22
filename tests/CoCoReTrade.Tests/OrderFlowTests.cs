using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Tests;

public sealed class OrderFlowTests
{
    private const string Seller = "seller-1", Buyer = "buyer-1", Other = "buyer-2", Admin = "admin-1";
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 5, 0, 0, TimeSpan.Zero);
    private const string PickupLocal = "2026-10-14T10:00";
    private static (Product Product, OrderService Service, MemoryOrders Store, ManualClock Clock) Setup(
        IEnumerable<ShippingZone>? zones = null, bool active = true)
    {
        var product = Product.Submit(Seller, "CC-2026-000300", "Áo khoác denim", "Zara", "Áo", "M",
            500_000, 300_000, 95, true,
            [new ProductImage("a.jpg", "image/jpeg", 0), new ProductImage("b.jpg", "image/jpeg", 1), new ProductImage("c.jpg", "image/jpeg", 2)]);
        if (active) { product.ReviewPreliminarily("admin-1", PreliminaryReviewStatus.Approved, null, Start.UtcDateTime); product.RecordInspection("inspector-1", true, product.Name, product.Brand, product.Category, product.Size, product.OriginalPrice, product.AskingPrice, 95, "Physical item checked", [], Start.UtcDateTime); product.Publish("CC-2026-000300", Start.UtcDateTime); }
        var store = new MemoryOrders(product);
        var clock = new ManualClock(Start);
        var service = new OrderService(store, new MarketplacePolicy(25, 15),
            new ShippingPolicy(zones, storeAddress: "12 Example Street, CoCo"), clock,
            permissions: new OrderPermissions());
        return (product, service, store, clock);
    }
    private static CheckoutRequest Pickup(Guid productId, Guid? offerId = null, Guid? key = null,
        string recipient = "Nguyen Van A") => new(productId, offerId, key ?? Guid.NewGuid(), recipient,
            "0905123456", FulfillmentType.Pickup, PickupLocal, null, null, null);
    private static CheckoutRequest Delivery(Guid productId, Guid? offerId = null, Guid? key = null,
        string province = "Tỉnh A", string ward = "Phường B") =>
        new(productId, offerId, key ?? Guid.NewGuid(), "Nguyen Van A", "0905123456",
            FulfillmentType.Shipping, null, province, ward, "12 Main Street");

    [Fact]
    public async Task BuyNowUsesListingPriceAndReservesOnlyAfterOrder()
    {
        var (product, service, store, _) = Setup();
        var quote = await service.QuoteAsync(product.Id, null, Buyer, FulfillmentType.Pickup, null, null, default);
        Assert.Equal(300_000, quote.MerchandisePrice);
        Assert.Equal(0, quote.ShippingFee);
        Assert.Equal(ProductStatus.Active, product.Status);
        var order = await service.PlaceAsync(Pickup(product.Id), Buyer, default);
        Assert.Equal(OrderStatus.PendingProcessing, order.Status);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(300_000, order.MerchandisePrice);
        Assert.Equal(45_000, order.CommissionAmount);
        Assert.Equal("CC-2026-000300", order.ProductSku);
        Assert.Equal("12 Example Street, CoCo", order.PickupAddress);
        Assert.Equal(ProductStatus.Reserved, product.Status);
        Assert.Single(store.Orders);
        Assert.Equal(3, store.Notifications.Count);
        Assert.All(store.Notifications, notification =>
        {
            Assert.Contains(product.Name, notification.Message);
            Assert.DoesNotContain(product.Sku, notification.Message);
        });
        Assert.Contains(store.Audits, x => x.Action == "OrderPlaced" && x.ActorId == Buyer);
    }

    [Fact]
    public async Task ShippingZonesUseProvinceAndWardAndCommissionExcludesShipping()
    {
        var (product, service, _, _) = Setup([new ShippingZone("Tỉnh A", "Phường B")]);
        var inner = await service.QuoteAsync(product.Id, null, Buyer, FulfillmentType.Shipping, "Tỉnh A", "Phường B", default);
        var outer = await service.QuoteAsync(product.Id, null, Buyer, FulfillmentType.Shipping, "Tỉnh A", "Phường khác", default);
        Assert.Equal(20_000, inner.ShippingFee);
        Assert.Equal(35_000, outer.ShippingFee);
        var order = await service.PlaceAsync(Delivery(product.Id), Buyer, default);
        Assert.Equal(320_000, order.TotalAmount);
        Assert.Equal(45_000, order.CommissionAmount);
        Assert.Equal("Phường B", order.Ward);
        Assert.Equal("12 Main Street", order.StreetAddress);
    }

    [Fact]
    public async Task PickupAndDeliveryInputsAreValidatedServerSide()
    {
        var (product, service, _, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => service.PlaceAsync(Pickup(product.Id) with { PickupLocal = "2026-10-12T10:00" }, Buyer, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.PlaceAsync(Pickup(product.Id) with { PickupLocal = "2026-10-14T23:00" }, Buyer, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.PlaceAsync(Pickup(product.Id) with { RecipientPhone = "123" }, Buyer, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.PlaceAsync(Delivery(product.Id) with { Ward = "" }, Buyer, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PlaceAsync(Pickup(product.Id), Seller, default));
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task AcceptedOfferIsExclusiveUnexpiredAndUsesFinalPrice()
    {
        var (product, service, store, clock) = Setup();
        var offer = Offer.Create(product, Buyer, 230_000, 225_000, 0, Start.UtcDateTime);
        offer.Counter(Seller, 270_000, 225_000, 1, product.Status, Start.UtcDateTime);
        offer.Accept(Buyer, product.Status, Start.UtcDateTime);
        store.Offers.Add(offer);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.QuoteAsync(product.Id, offer.Id, Other,
            FulfillmentType.Pickup, null, null, default));
        var quote = await service.QuoteAsync(product.Id, offer.Id, Buyer, FulfillmentType.Shipping, "Tỉnh X", "Xã Y", default);
        Assert.Equal(270_000, quote.MerchandisePrice);
        Assert.Equal(35_000, quote.ShippingFee);
        Assert.Equal(40_500, quote.CommissionAmount);
        Assert.Equal(ProductStatus.Active, product.Status);
        var order = await service.PlaceAsync(Delivery(product.Id, offer.Id, province: "Tỉnh X", ward: "Xã Y"), Buyer, default);
        Assert.Equal(305_000, order.TotalAmount);
        Assert.Equal(40_500, order.CommissionAmount);
        Assert.Equal(OfferStatus.Used, offer.Status);
        Assert.Equal(ProductStatus.Reserved, product.Status);

        var (p2, s2, st2, c2) = Setup();
        var expired = Offer.Create(p2, Buyer, 230_000, 225_000, 0, Start.UtcDateTime);
        expired.Accept(Seller, p2.Status, Start.UtcDateTime);
        st2.Offers.Add(expired); c2.Advance(TimeSpan.FromHours(24));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s2.PlaceAsync(Pickup(p2.Id, expired.Id), Buyer, default));
    }

    [Fact]
    public async Task RepeatedIdempotencyKeyReturnsSameSnapshotAndDifferentPayloadConflicts()
    {
        var (product, service, store, _) = Setup();
        var request = Pickup(product.Id);
        var first = await service.PlaceAsync(request, Buyer, default);
        var second = await service.PlaceAsync(request, Buyer, default);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(store.Orders);
        Assert.Equal(3, store.Notifications.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PlaceAsync(request with { RecipientName = "Different Person" }, Buyer, default));
    }

    [Fact]
    public async Task ConcurrentBuyersCannotBuyTheSamePhysicalItem()
    {
        var (product, service, store, _) = Setup();
        var attempts = await Task.WhenAll(new[] { Buyer, Other }.Select(async buyer =>
        {
            try { await service.PlaceAsync(Pickup(product.Id), buyer, default); return true; }
            catch (InvalidOperationException) { return false; }
        }));
        Assert.Single(attempts, x => x);
        Assert.Single(store.Orders);
        Assert.Equal(ProductStatus.Reserved, product.Status);
    }

    [Fact]
    public async Task ConcurrentIdenticalSubmissionsReturnOneOrder()
    {
        var (product, service, store, _) = Setup();
        var request = Pickup(product.Id);
        var results = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => service.PlaceAsync(request, Buyer, default)));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(store.Orders);
        Assert.Equal(3, store.Notifications.Count);
    }

    [Fact]
    public async Task UnpaidCancellationRestocksAndAllowsAnotherBuyer()
    {
        var (product, service, store, _) = Setup();
        var first = await service.PlaceAsync(Pickup(product.Id), Buyer, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CancelAsync(first.Id, Other, false, default));
        var cancelled = await service.CancelAsync(first.Id, Buyer, false, default);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(ProductStatus.Active, product.Status);
        var second = await service.PlaceAsync(Pickup(product.Id), Other, default);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, store.Orders.Count);
    }

    [Fact]
    public async Task CompletionRequiresPaymentAndNeverCreditsWalletOnPlacement()
    {
        var (product, service, _, _) = Setup();
        var placed = await service.PlaceAsync(Pickup(product.Id), Buyer, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(placed.Id, Admin, default));
        await service.MarkReadyAsync(placed.Id, Admin, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(placed.Id, Admin, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(placed.Id, Buyer, false, default));
        var paid = await service.ConfirmPaymentAsync(placed.Id, Admin, "CASH-1234", default);
        Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(placed.Id, Admin, true, default));
        var completed = await service.CompleteAsync(placed.Id, Admin, default);
        Assert.Equal(OrderStatus.Completed, completed.Status);
        Assert.Equal(ProductStatus.Sold, product.Status);
        Assert.Contains((await service.AdminQueueAsync(default)), x => x.Id == placed.Id && x.PaymentStatus == PaymentStatus.Paid);
    }

    [Fact]
    public async Task AdminMayCancelReadyUnpaidPickupAndRestock()
    {
        var (product, service, _, _) = Setup();
        var placed = await service.PlaceAsync(Pickup(product.Id), Buyer, default);
        await service.MarkReadyAsync(placed.Id, Admin, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(placed.Id, Buyer, false, default));
        var cancelled = await service.CancelAsync(placed.Id, Admin, true, default);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task PaymentCanBeConfirmedWithoutAReceiptCode()
    {
        var (product, service, _, _) = Setup();
        var placed = await service.PlaceAsync(Pickup(product.Id), Buyer, default);
        await service.MarkReadyAsync(placed.Id, Admin, default);
        var paid = await service.ConfirmPaymentAsync(placed.Id, Admin, null, default);
        Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
        Assert.Null(paid.PaymentReference);
    }

    [Fact]
    public async Task ShippingCanBeHandedOffDirectlyWithTrackingCode()
    {
        var (product, service, store, _) = Setup();
        var placed = await service.PlaceAsync(Delivery(product.Id), Buyer, default);
        var handedOff = await service.MarkReadyAsync(placed.Id, Admin, default, "SHIP-789012");
        Assert.Equal(OrderStatus.Shipping, handedOff.Status);
        Assert.Equal("SHIP-789012", handedOff.TrackingCode);
        Assert.Contains(store.Notifications, x => x.UserId == Buyer && x.Message.Contains("SHIP-789012"));
        Assert.Contains(store.Notifications, x => x.UserId == Seller && x.Message.Contains("SHIP-789012"));
        var timeline = (await service.DetailsAsync(placed.Id, Buyer, false, default))!.Timeline!;
        Assert.Single(timeline, x => x.Action == "OrderConfirmed");
        Assert.Single(timeline, x => x.Action == "OrderPrepared");
        var paid = await service.ConfirmPaymentAsync(placed.Id, Admin, null, default);
        Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
    }

    [Fact]
    public async Task SellerSeesOrderStatusWithoutBuyerContactDetails()
    {
        var (product, service, _, _) = Setup();
        var placed = await service.PlaceAsync(Delivery(product.Id), Buyer, default);
        var sellerView = await service.DetailsAsync(placed.Id, Seller, false, default);
        Assert.NotNull(sellerView);
        Assert.Equal(OrderStatus.PendingProcessing, sellerView.Status);
        Assert.Equal("", sellerView.RecipientPhone);
        Assert.Null(sellerView.StreetAddress);
        Assert.Equal(Guid.Empty, sellerView.IdempotencyKey);
        Assert.Null(await service.DetailsAsync(placed.Id, "outsider", false, default));
        Assert.Equal("12 Main Street", (await service.DetailsAsync(placed.Id, Buyer, false, default))!.StreetAddress);
    }

    [Fact]
    public async Task ConfirmationAndCarrierHandoffHaveSeparateTimelineTimes()
    {
        var (product, service, _, clock) = Setup();
        var placed = await service.PlaceAsync(Delivery(product.Id), Buyer, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConfirmAsync(placed.Id, Buyer, default));

        clock.Advance(TimeSpan.FromMinutes(10));
        await service.ConfirmAsync(placed.Id, Admin, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(placed.Id, Admin, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(placed.Id, Buyer, false, default));
        clock.Advance(TimeSpan.FromMinutes(20));
        await Assert.ThrowsAsync<ArgumentException>(() => service.MarkReadyAsync(placed.Id, Admin, default));
        await service.MarkReadyAsync(placed.Id, Admin, default, "SHIP-123456");

        var buyerView = (await service.DetailsAsync(placed.Id, Buyer, false, default))!;
        var sellerView = (await service.DetailsAsync(placed.Id, Seller, false, default))!;
        Assert.Equal("SHIP-123456", buyerView.TrackingCode);
        Assert.Equal("SHIP-123456", sellerView.TrackingCode);
        var timeline = buyerView.Timeline!;
        Assert.Equal(Start.AddMinutes(10).UtcDateTime,
            Assert.Single(timeline, x => x.Action == "OrderConfirmed").CreatedAtUtc);
        Assert.Equal(Start.AddMinutes(30).UtcDateTime,
            Assert.Single(timeline, x => x.Action == "OrderPrepared").CreatedAtUtc);
    }

    [Fact]
    public async Task OrderAdministrationRequiresPermissionInsideService()
    {
        var (product, service, _, _) = Setup();
        var placed = await service.PlaceAsync(Pickup(product.Id), Buyer, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.MarkReadyAsync(placed.Id, Buyer, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConfirmPaymentAsync(placed.Id, Buyer, "CASH-1234", default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CompleteAsync(placed.Id, Buyer, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CancelAsync(placed.Id, Buyer, true, default));
        Assert.Equal(OrderStatus.PendingProcessing, (await service.DetailsAsync(placed.Id, Buyer, false, default))!.Status);
    }

    private sealed class OrderPermissions : IStaffPermissionChecker
    {
        public Task EnsureAsync(string actorId, string permission, CancellationToken ct) =>
            actorId == Admin && permission == StaffPermissions.ManageOrders
                ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException("Admin permission required."));
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset value = now;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan span) => value = value.Add(span);
    }
    private sealed class MemoryOrders(Product product) : IOrderRepository
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        public List<Order> Orders { get; } = [];
        public List<Offer> Offers { get; } = [];
        public List<MarketplaceNotification> Notifications { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
        { await gate.WaitAsync(ct); try { return await work(); } finally { gate.Release(); } }
        public Task<Product?> ProductAsync(Guid id, CancellationToken ct) => Task.FromResult<Product?>(id == product.Id ? product : null);
        public Task<Offer?> OfferAsync(Guid id, CancellationToken ct) => Task.FromResult<Offer?>(Offers.FirstOrDefault(x => x.Id == id));
        public Task<Order?> OrderAsync(Guid id, CancellationToken ct) => Task.FromResult<Order?>(Orders.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<OrderTimelineEvent>> TimelineAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrderTimelineEvent>>(Audits.Where(x => x.EntityType == "Order" && x.EntityId == id.ToString())
                .Select(x => new OrderTimelineEvent(x.Action, x.CreatedAtUtc)).ToList());
        public Task<Order?> ByIdempotencyAsync(string buyerId, Guid key, CancellationToken ct) =>
            Task.FromResult<Order?>(Orders.FirstOrDefault(x => x.BuyerId == buyerId && x.IdempotencyKey == key));
        public Task<IReadOnlyList<Order>> MineAsync(string userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Order>>(Orders.Where(x => x.BuyerId == userId || x.SellerId == userId).ToList());
        public Task<IReadOnlyList<Order>> AdminQueueAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Order>>(Orders);
        public Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([Admin]);
        public Task<IReadOnlyList<Guid>> DueUnpaidOrdersAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>>(Orders.Where(x => x.Status == OrderStatus.PendingProcessing &&
                x.PaymentStatus == PaymentStatus.Unpaid && x.CreatedAtUtc <= cutoffUtc)
                .Take(batchSize).Select(x => x.Id).ToList());
        public Task AddAsync(Order order, CancellationToken ct) { Orders.Add(order); return Task.CompletedTask; }
        public void AddOfferEvent(OfferEvent offerEvent) { }
        public void AddNotification(MarketplaceNotification notification) => Notifications.Add(notification);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
