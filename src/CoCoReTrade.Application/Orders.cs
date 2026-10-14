using System.Globalization;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record ShippingZone(string Province, string Ward);

public sealed class ShippingPolicy
{
    private readonly HashSet<string> innerZones;
    public int StoreUtcOffsetHours { get; }
    public int OpeningHour { get; }
    public int ClosingHour { get; }
    public string StoreAddress { get; }
    public bool CanPickup => !string.IsNullOrWhiteSpace(StoreAddress) &&
        !StoreAddress.StartsWith("SET_", StringComparison.OrdinalIgnoreCase);

    public ShippingPolicy(IEnumerable<ShippingZone>? innerCityZones = null, int storeUtcOffsetHours = 7,
        int openingHour = 9, int closingHour = 21, string storeAddress = "")
    {
        if (storeUtcOffsetHours is < -12 or > 14 || openingHour is < 0 or > 23 || closingHour is < 1 or > 24 || openingHour >= closingHour)
            throw new ArgumentException("Invalid store pickup hours or UTC offset.");
        StoreUtcOffsetHours = storeUtcOffsetHours;
        OpeningHour = openingHour;
        ClosingHour = closingHour;
        StoreAddress = storeAddress;
        innerZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in innerCityZones ?? [])
        {
            if (string.IsNullOrWhiteSpace(zone.Province) || string.IsNullOrWhiteSpace(zone.Ward))
                throw new ArgumentException("Inner-city shipping zones require province and ward.");
            innerZones.Add(Key(zone.Province, zone.Ward));
        }
    }
    private static string Key(string province, string ward) => $"{province.Trim()}|{ward.Trim()}";
    public decimal Fee(FulfillmentType fulfillment, string? province, string? ward)
    {
        if (fulfillment == FulfillmentType.Pickup) return 0;
        if (fulfillment != FulfillmentType.Shipping) throw new ArgumentOutOfRangeException(nameof(fulfillment));
        if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(ward))
            throw new ArgumentException("Province/city and ward/commune are required.");
        return ShippingFeeRules.Calculate(fulfillment, innerZones.Contains(Key(province, ward)));
    }
    public DateTime ParsePickupUtc(string? localTime, DateTime nowUtc)
    {
        if (!CanPickup) throw new InvalidOperationException("Store pickup address is not configured.");
        if (!DateTime.TryParseExact(localTime, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)) throw new ArgumentException("Choose a pickup date and time.");
        if (local.Hour < OpeningHour || local.Hour >= ClosingHour)
            throw new ArgumentException($"Pickup is available from {OpeningHour:00}:00 to {ClosingHour:00}:00.");
        var offset = TimeSpan.FromHours(StoreUtcOffsetHours);
        var utc = new DateTimeOffset(local, offset).UtcDateTime;
        var todayAtStore = new DateTimeOffset(nowUtc, TimeSpan.Zero).ToOffset(offset).Date;
        if (utc <= nowUtc || local.Date < todayAtStore.AddDays(1) || local.Date > todayAtStore.AddDays(3))
            throw new ArgumentException("Choose a future pickup time within the next 1–3 days.");
        return utc;
    }
}

public sealed record CheckoutRequest(Guid ProductId, Guid? OfferId, Guid IdempotencyKey,
    string RecipientName, string RecipientPhone, FulfillmentType Fulfillment,
    string? PickupLocal, string? Province, string? Ward, string? StreetAddress,
    DeliveryWindow? PreferredDeliveryWindow = null, string? DeliveryNote = null);
public sealed record CheckoutQuote(Guid ProductId, Guid? OfferId, string ProductSku, string ProductName,
    string ProductSize, decimal MerchandisePrice, decimal ShippingFee, decimal TotalAmount,
    decimal CommissionAmount, DateTime? OfferExpiresAtUtc, bool ShippingFeePending = false);
public sealed record OrderDto(Guid Id, Guid ProductId, Guid? OfferId, string ProductSku, string ProductName,
    string ProductSize, string BuyerId, string SellerId, Guid IdempotencyKey, FulfillmentType Fulfillment,
    string RecipientName, string RecipientPhone, string? Province, string? Ward, string? StreetAddress,
    DateTime? PickupAtUtc, string? PickupAddress, decimal MerchandisePrice, decimal ShippingFee, decimal TotalAmount,
    decimal CommissionAmount, OrderStatus Status, PaymentStatus PaymentStatus, string? PaymentReference,
    DateTime CreatedAtUtc, DateTime? PaidAtUtc, DateTime? CancelledAtUtc, DateTime? CompletedAtUtc,
    DeliveryWindow? PreferredDeliveryWindow = null, string? DeliveryNote = null);

public sealed class OrderConcurrencyException(string message) : InvalidOperationException(message);

public interface IOrderRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
    Task<Product?> ProductAsync(Guid id, CancellationToken ct);
    Task<Offer?> OfferAsync(Guid id, CancellationToken ct);
    Task<Order?> OrderAsync(Guid id, CancellationToken ct);
    Task<Order?> ByIdempotencyAsync(string buyerId, Guid key, CancellationToken ct);
    Task<IReadOnlyList<Order>> MineAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<Order>> AdminQueueAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueUnpaidOrdersAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    void AddOfferEvent(OfferEvent offerEvent);
    void AddNotification(MarketplaceNotification notification);
    void AddAudit(AuditLog audit);
    Task SaveAsync(CancellationToken ct);
}

public interface IOrderService
{
    Task<CheckoutQuote> QuoteAsync(Guid productId, Guid? offerId, string buyerId, FulfillmentType fulfillment,
        string? province, string? ward, CancellationToken ct);
    Task<OrderDto> PlaceAsync(CheckoutRequest request, string buyerId, CancellationToken ct);
    Task<OrderDto?> DetailsAsync(Guid id, string viewerId, bool isAdmin, CancellationToken ct);
    Task<IReadOnlyList<OrderDto>> MineAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<OrderDto>> AdminQueueAsync(CancellationToken ct);
    Task<OrderDto> CancelAsync(Guid id, string actorId, bool isAdmin, CancellationToken ct);
    Task<OrderDto> MarkReadyAsync(Guid id, string adminId, CancellationToken ct);
    Task<OrderDto> ConfirmPaymentAsync(Guid id, string adminId, string reference, CancellationToken ct);
    Task<OrderDto> CompleteAsync(Guid id, string adminId, CancellationToken ct);
}

public sealed class OrderService(IOrderRepository repository, MarketplacePolicy marketplace,
    ShippingPolicy shipping, TimeProvider clock, IShippingFeeService? shippingFees = null,
    ICommissionService? commissions = null, IProductLifecycleService? products = null,
    IOrderLifecycleService? lifecycle = null, IStaffPermissionChecker? permissions = null) : IOrderService
{
    public async Task<CheckoutQuote> QuoteAsync(Guid productId, Guid? offerId, string buyerId,
        FulfillmentType fulfillment, string? province, string? ward, CancellationToken ct)
    {
        var product = await repository.ProductAsync(productId, ct) ?? throw new KeyNotFoundException("Product not found.");
        var offer = await ResolveOffer(offerId, product, buyerId, ct);
        EnsureAvailable(product, buyerId);
        if (fulfillment == FulfillmentType.Pickup && !shipping.CanPickup)
            throw new InvalidOperationException("Store pickup address is not configured.");
        var item = offer?.AcceptedPrice ?? product.AskingPrice;
        var feePending = fulfillment == FulfillmentType.Shipping &&
            (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(ward));
        var fee = feePending ? 0 : shippingFees is null ? shipping.Fee(fulfillment, province, ward) :
            await shippingFees.FeeAsync(fulfillment, province, ward, ct);
        return new CheckoutQuote(product.Id, offer?.Id, product.Sku, product.Name, product.Size,
            item, fee, item + fee,
            commissions?.Calculate(item) ?? CommissionRules.Calculate(item, marketplace.CommissionPercent),
            offer?.ExpiresAtUtc, feePending);
    }

    public async Task<OrderDto> PlaceAsync(CheckoutRequest request, string buyerId, CancellationToken ct)
    {
        if (request.ProductId == Guid.Empty || request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(buyerId) ||
            string.IsNullOrWhiteSpace(request.RecipientName) || string.IsNullOrWhiteSpace(request.RecipientPhone))
            throw new ArgumentException("Product, idempotency key and recipient are required.");
        try
        {
            return await repository.InTransactionAsync(async () =>
            {
                var existing = await repository.ByIdempotencyAsync(buyerId, request.IdempotencyKey, ct);
                if (existing is not null) return Replay(existing, request);
                var product = await repository.ProductAsync(request.ProductId, ct) ?? throw new KeyNotFoundException("Product not found.");
                var offer = await ResolveOffer(request.OfferId, product, buyerId, ct);
                EnsureAvailable(product, buyerId);
                var now = clock.GetUtcNow().UtcDateTime;
                var pickup = request.Fulfillment == FulfillmentType.Pickup
                    ? shipping.ParsePickupUtc(request.PickupLocal, now) : (DateTime?)null;
                if (request.Fulfillment == FulfillmentType.Shipping && !string.IsNullOrWhiteSpace(request.PickupLocal))
                    throw new ArgumentException("Pickup time cannot be used for delivery.");
                var fee = shippingFees is null ? shipping.Fee(request.Fulfillment, request.Province, request.Ward) :
                    await shippingFees.FeeAsync(request.Fulfillment, request.Province, request.Ward, ct);
                var order = Order.Create(product, offer, buyerId, request.IdempotencyKey, request.Fulfillment,
                    request.RecipientName, request.RecipientPhone, request.Province, request.Ward,
                    request.StreetAddress, pickup, shipping.StoreAddress, fee, marketplace.CommissionPercent, now,
                    request.PreferredDeliveryWindow, request.DeliveryNote);
                if (offer is not null)
                {
                    offer.UseForOrder(buyerId, product.Status, now);
                    repository.AddOfferEvent(offer.Events[^1]);
                }
                if (products is null) product.Reserve(); else products.ReserveForOrder(product, order);
                await repository.AddAsync(order, ct);
                Audit(buyerId, order, "OrderPlaced", now);
                Notify(order.SellerId, order, $"{order.ProductSku} đã có đơn mới ({order.Fulfillment}).", now);
                Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã được ghi nhận, đang chờ xử lý.", now);
                foreach (var adminId in await repository.AdminUserIdsAsync(ct))
                {
                    if (adminId == order.SellerId || adminId == order.BuyerId) continue;
                    Notify(adminId, order, $"Đơn mới {order.ProductSku}: {order.Fulfillment}, phí giao {order.ShippingFee:N0}đ.", now);
                }
                await repository.SaveAsync(ct);
                return ToDto(order);
            }, ct);
        }
        catch (OrderConcurrencyException)
        {
            var existing = await repository.ByIdempotencyAsync(buyerId, request.IdempotencyKey, ct);
            if (existing is not null) return Replay(existing, request);
            throw;
        }
    }

    private OrderDto Replay(Order existing, CheckoutRequest request)
    {
        var pickup = request.Fulfillment == FulfillmentType.Pickup
            ? shipping.ParsePickupUtc(request.PickupLocal, existing.CreatedAtUtc.AddTicks(-1)) : (DateTime?)null;
        if (!existing.MatchesSubmission(request.ProductId, request.OfferId, request.Fulfillment,
                request.RecipientName, request.RecipientPhone, request.Province, request.Ward,
                request.StreetAddress, pickup, request.PreferredDeliveryWindow, request.DeliveryNote))
            throw new InvalidOperationException("Idempotency key was already used for different order details.");
        return ToDto(existing);
    }
    private static void EnsureAvailable(Product product, string buyerId)
    {
        if (product.SellerId == buyerId) throw new UnauthorizedAccessException("Cannot buy your own product.");
        if (product.Status is ProductStatus.PendingApproval or ProductStatus.Rejected)
            throw new KeyNotFoundException("Product not found.");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("Product is no longer available.");
    }
    private async Task<Offer?> ResolveOffer(Guid? offerId, Product product, string buyerId, CancellationToken ct)
    {
        if (offerId is null) return null;
        var offer = await repository.OfferAsync(offerId.Value, ct) ?? throw new KeyNotFoundException("Offer not found.");
        if (offer.ProductId != product.Id || offer.BuyerId != buyerId) throw new UnauthorizedAccessException("Offer belongs to another buyer or product.");
        if (offer.Status != OfferStatus.Accepted || offer.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || offer.AcceptedPrice is null)
            throw new InvalidOperationException("Accepted offer has expired or is unavailable.");
        return offer;
    }
    public async Task<OrderDto?> DetailsAsync(Guid id, string viewerId, bool isAdmin, CancellationToken ct)
    {
        var order = await repository.OrderAsync(id, ct);
        return order is null || !isAdmin && viewerId != order.BuyerId && viewerId != order.SellerId
            ? null : ForViewer(order, viewerId, isAdmin);
    }
    public async Task<IReadOnlyList<OrderDto>> MineAsync(string userId, CancellationToken ct) =>
        (await repository.MineAsync(userId, ct)).Select(x => ForViewer(x, userId, false)).ToList();
    public async Task<IReadOnlyList<OrderDto>> AdminQueueAsync(CancellationToken ct) =>
        (await repository.AdminQueueAsync(ct)).Select(ToDto).ToList();

    public async Task<OrderDto> CancelAsync(Guid id, string actorId, bool isAdmin, CancellationToken ct)
    {
        if (isAdmin && actorId != "system")
            await EnsureOrderAdminAsync(actorId, ct);
        return await ChangeAsync(id, async (order, product, now) =>
        {
            if (!isAdmin && order.BuyerId != actorId) throw new UnauthorizedAccessException("Only the buyer or an admin can cancel.");
            if (lifecycle is null) order.Cancel(product, isAdmin, now);
            else lifecycle.Cancel(order, product, isAdmin, now);
            Audit(actorId, order, "OrderCancelled", now);
            Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã hủy; sản phẩm mở bán lại.", now);
            Notify(order.SellerId, order, $"Đơn {order.ProductSku} đã hủy; sản phẩm mở bán lại.", now);
            foreach (var adminId in await repository.AdminUserIdsAsync(ct))
            {
                if (adminId == order.BuyerId || adminId == order.SellerId) continue;
                Notify(adminId, order, $"Đơn {order.ProductSku} đã hủy bởi {actorId}.", now);
            }
        }, ct);
    }
    public async Task<OrderDto> MarkReadyAsync(Guid id, string adminId, CancellationToken ct)
    {
        await EnsureOrderAdminAsync(adminId, ct);
        return await ChangeAsync(id, (order, _, now) =>
        {
            if (lifecycle is null) order.MarkReady(); else lifecycle.MarkReady(order);
            Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã sẵn sàng: {order.Status}.", now);
            Audit(adminId, order, "OrderPrepared", now);
            Notify(order.SellerId, order, $"Đơn {order.ProductSku} chuyển sang {order.Status}.", now);
            return Task.CompletedTask;
        }, ct);
    }
    public async Task<OrderDto> ConfirmPaymentAsync(Guid id, string adminId, string reference, CancellationToken ct)
    {
        await EnsureOrderAdminAsync(adminId, ct);
        return await ChangeAsync(id, (order, _, now) =>
        {
            if (lifecycle is null) order.ConfirmPayment(adminId, reference, now);
            else lifecycle.ConfirmPayment(order, adminId, reference, now);
            Audit(adminId, order, "PaymentConfirmed", now);
            Notify(order.BuyerId, order, $"CoCo đã xác nhận thanh toán cho đơn {order.ProductSku}.", now);
            Notify(order.SellerId, order, $"CoCo đã xác nhận thanh toán cho {order.ProductSku}.", now);
            return Task.CompletedTask;
        }, ct);
    }
    public async Task<OrderDto> CompleteAsync(Guid id, string adminId, CancellationToken ct)
    {
        await EnsureOrderAdminAsync(adminId, ct);
        return await ChangeAsync(id, (order, product, now) =>
        {
            if (lifecycle is null) order.Complete(product, now);
            else lifecycle.Complete(order, product, now);
            Audit(adminId, order, "OrderCompleted", now);
            Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã hoàn tất.", now);
            Notify(order.SellerId, order, $"{order.ProductSku} đã bán thành công. Ví chưa được ghi có tự động.", now);
            return Task.CompletedTask;
        }, ct);
    }
    private Task EnsureOrderAdminAsync(string actorId, CancellationToken ct) =>
        permissions?.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct) ??
        throw new InvalidOperationException("Order administration requires a permission checker.");
    private Task<OrderDto> ChangeAsync(Guid id, Func<Order, Product, DateTime, Task> change, CancellationToken ct) =>
        repository.InTransactionAsync(async () =>
        {
            var order = await repository.OrderAsync(id, ct) ?? throw new KeyNotFoundException("Order not found.");
            var product = await repository.ProductAsync(order.ProductId, ct) ?? throw new KeyNotFoundException("Product not found.");
            await change(order, product, clock.GetUtcNow().UtcDateTime);
            await repository.SaveAsync(ct);
            return ToDto(order);
        }, ct);
    private void Notify(string userId, Order order, string message, DateTime now) =>
        repository.AddNotification(new MarketplaceNotification { Id = Guid.NewGuid(), UserId = userId,
            OrderId = order.Id, Category = "orders", Message = message, CreatedAtUtc = now });
    private void Audit(string actorId, Order order, string action, DateTime now) =>
        repository.AddAudit(new AuditLog { Id = Guid.NewGuid(), ActorId = actorId, Action = action,
            EntityType = "Order", EntityId = order.Id.ToString(), CreatedAtUtc = now });
    private static OrderDto ToDto(Order x) => new(x.Id, x.ProductId, x.OfferId, x.ProductSku, x.ProductName,
        x.ProductSize, x.BuyerId, x.SellerId, x.IdempotencyKey, x.Fulfillment, x.RecipientName, x.RecipientPhone,
        x.Province, x.Ward, x.StreetAddress, x.PickupAtUtc, x.PickupAddress, x.ItemAmount, x.ShippingFee, x.TotalAmount,
        x.CommissionAmount, x.Status, x.PaymentStatus, x.PaymentReference,
        x.CreatedAtUtc, x.PaidAtUtc, x.CancelledAtUtc, x.CompletedAtUtc,
        x.PreferredDeliveryWindow, x.DeliveryNote);
    private static OrderDto ForViewer(Order x, string viewerId, bool isAdmin)
    {
        var dto = ToDto(x);
        return isAdmin || x.BuyerId == viewerId ? dto : dto with
        {
            BuyerId = "", IdempotencyKey = Guid.Empty, RecipientName = "CoCo phụ trách", RecipientPhone = "",
            Province = null, Ward = null, StreetAddress = null, PickupAtUtc = null,
            PickupAddress = null, PaymentReference = null
        };
    }
}
