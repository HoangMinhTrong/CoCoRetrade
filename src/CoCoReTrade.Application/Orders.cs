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
            throw new ArgumentException("Giờ nhận tại cửa hàng hoặc múi giờ không hợp lệ.");
        StoreUtcOffsetHours = storeUtcOffsetHours;
        OpeningHour = openingHour;
        ClosingHour = closingHour;
        StoreAddress = storeAddress;
        innerZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in innerCityZones ?? [])
        {
            if (string.IsNullOrWhiteSpace(zone.Province) || string.IsNullOrWhiteSpace(zone.Ward))
                throw new ArgumentException("Khu vực nội thành cần có tỉnh/thành và phường/xã.");
            innerZones.Add(Key(zone.Province, zone.Ward));
        }
    }
    private static string Key(string province, string ward) => $"{province.Trim()}|{ward.Trim()}";
    public decimal Fee(FulfillmentType fulfillment, string? province, string? ward)
    {
        if (fulfillment == FulfillmentType.Pickup) return 0;
        if (fulfillment != FulfillmentType.Shipping) throw new ArgumentOutOfRangeException(nameof(fulfillment));
        if (string.IsNullOrWhiteSpace(province) || string.IsNullOrWhiteSpace(ward))
            throw new ArgumentException("Cần nhập tỉnh/thành và phường/xã.");
        return ShippingFeeRules.Calculate(fulfillment, innerZones.Contains(Key(province, ward)));
    }
    public DateTime ParsePickupUtc(string? localTime, DateTime nowUtc)
    {
        var (local, utc, offset) = ParseStoreTime(localTime);
        var todayAtStore = new DateTimeOffset(nowUtc, TimeSpan.Zero).ToOffset(offset).Date;
        if (utc <= nowUtc || local.Date < todayAtStore.AddDays(1) || local.Date > todayAtStore.AddDays(3))
            throw new ArgumentException("Hãy chọn giờ nhận hàng trong 1 đến 3 ngày tới.");
        return utc;
    }

    public DateTime ParseOfferPickupUtc(string? localTime, DateTime nowUtc, DateTime deadlineUtc)
    {
        var (_, utc, _) = ParseStoreTime(localTime);
        if (utc <= nowUtc || utc >= deadlineUtc)
            throw new ArgumentException("Hãy chọn giờ nhận tại cửa hàng trước khi giá thỏa thuận hết hạn sau 24 giờ.");
        return utc;
    }

    private (DateTime Local, DateTime Utc, TimeSpan Offset) ParseStoreTime(string? localTime)
    {
        if (!CanPickup) throw new InvalidOperationException("Chưa cấu hình địa chỉ nhận hàng tại CoCo.");
        if (!DateTime.TryParseExact(localTime, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local)) throw new ArgumentException("Hãy chọn ngày và giờ nhận hàng.");
        if (local.Hour < OpeningHour || local.Hour >= ClosingHour)
            throw new ArgumentException($"Có thể nhận hàng từ {OpeningHour:00}:00 đến {ClosingHour:00}:00.");
        var offset = TimeSpan.FromHours(StoreUtcOffsetHours);
        return (local, new DateTimeOffset(local, offset).UtcDateTime, offset);
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
    DeliveryWindow? PreferredDeliveryWindow = null, string? DeliveryNote = null,
    DateTime? PickupDeadlineAtUtc = null);

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
    Task<OrderDto> ReturnUndeliveredAsync(Guid id, string adminId, bool itemReturnedToStore, CancellationToken ct);
    Task<OrderDto> MarkReadyAsync(Guid id, string adminId, CancellationToken ct);
    Task<OrderDto> ConfirmPaymentAsync(Guid id, string adminId, string reference, CancellationToken ct);
    Task<OrderDto> CompleteAsync(Guid id, string adminId, CancellationToken ct);
}

public sealed class OrderService(IOrderRepository repository, MarketplacePolicy marketplace,
    ShippingPolicy shipping, TimeProvider clock, IShippingFeeService? shippingFees = null,
    ICommissionService? commissions = null, IProductLifecycleService? products = null,
    IOrderLifecycleService? lifecycle = null, IStaffPermissionChecker? permissions = null,
    ISellerPayoutService? payouts = null) : IOrderService
{
    public async Task<CheckoutQuote> QuoteAsync(Guid productId, Guid? offerId, string buyerId,
        FulfillmentType fulfillment, string? province, string? ward, CancellationToken ct)
    {
        var product = await repository.ProductAsync(productId, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
        var offer = await ResolveOffer(offerId, product, buyerId, ct);
        EnsureAvailable(product, buyerId);
        if (fulfillment == FulfillmentType.Pickup && !shipping.CanPickup)
            throw new InvalidOperationException("Chưa cấu hình địa chỉ nhận hàng tại CoCo.");
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
            throw new ArgumentException("Cần chọn sản phẩm và nhập thông tin người nhận.");
        try
        {
            return await repository.InTransactionAsync(async () =>
            {
                var existing = await repository.ByIdempotencyAsync(buyerId, request.IdempotencyKey, ct);
                if (existing is not null) return Replay(existing, request);
                var product = await repository.ProductAsync(request.ProductId, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
                var offer = await ResolveOffer(request.OfferId, product, buyerId, ct);
                EnsureAvailable(product, buyerId);
                var now = clock.GetUtcNow().UtcDateTime;
                var pickup = request.Fulfillment == FulfillmentType.Pickup
                    ? offer is null ? shipping.ParsePickupUtc(request.PickupLocal, now)
                        : shipping.ParseOfferPickupUtc(request.PickupLocal, now, offer.ExpiresAtUtc)
                    : (DateTime?)null;
                if (request.Fulfillment == FulfillmentType.Shipping && !string.IsNullOrWhiteSpace(request.PickupLocal))
                    throw new ArgumentException("Giao tận nơi không dùng giờ nhận tại cửa hàng.");
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
                Notify(order.SellerId, order, $"{order.ProductSku} đã có đơn mới ({FulfillmentName(order.Fulfillment)}).", now);
                Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã được ghi nhận, đang chờ xử lý.", now);
                foreach (var adminId in await repository.AdminUserIdsAsync(ct))
                {
                    if (adminId == order.SellerId || adminId == order.BuyerId) continue;
                    Notify(adminId, order, $"Đơn mới {order.ProductSku}: {FulfillmentName(order.Fulfillment)}, phí giao {order.ShippingFee:N0} VNĐ.", now);
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
            ? existing.PickupDeadlineAtUtc is { } deadline
                ? shipping.ParseOfferPickupUtc(request.PickupLocal, existing.CreatedAtUtc.AddTicks(-1), deadline)
                : shipping.ParsePickupUtc(request.PickupLocal, existing.CreatedAtUtc.AddTicks(-1))
            : (DateTime?)null;
        if (!existing.MatchesSubmission(request.ProductId, request.OfferId, request.Fulfillment,
                request.RecipientName, request.RecipientPhone, request.Province, request.Ward,
                request.StreetAddress, pickup, request.PreferredDeliveryWindow, request.DeliveryNote))
            throw new InvalidOperationException("Yêu cầu đặt hàng này đã được dùng với thông tin khác.");
        return ToDto(existing);
    }
    private static void EnsureAvailable(Product product, string buyerId)
    {
        if (product.SellerId == buyerId) throw new UnauthorizedAccessException("Không thể mua sản phẩm của chính mình.");
        if (product.Status is ProductStatus.PendingApproval or ProductStatus.Rejected)
            throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
        if (product.Status != ProductStatus.Active) throw new InvalidOperationException("Sản phẩm không còn mở bán.");
    }
    private async Task<Offer?> ResolveOffer(Guid? offerId, Product product, string buyerId, CancellationToken ct)
    {
        if (offerId is null) return null;
        var offer = await repository.OfferAsync(offerId.Value, ct) ?? throw new KeyNotFoundException("Không tìm thấy đề xuất giá.");
        if (offer.ProductId != product.Id || offer.BuyerId != buyerId) throw new UnauthorizedAccessException("Đề xuất giá thuộc về người mua hoặc sản phẩm khác.");
        if (offer.Status != OfferStatus.Accepted || offer.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime || offer.AcceptedPrice is null)
            throw new InvalidOperationException("Giá thỏa thuận đã hết hạn hoặc không còn áp dụng.");
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
            if (!isAdmin && order.BuyerId != actorId) throw new UnauthorizedAccessException("Chỉ người mua hoặc quản trị viên mới được hủy đơn.");
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
    public async Task<OrderDto> ReturnUndeliveredAsync(Guid id, string adminId, bool itemReturnedToStore, CancellationToken ct)
    {
        await EnsureOrderAdminAsync(adminId, ct);
        if (!itemReturnedToStore)
            throw new ArgumentException("Hãy xác nhận hiện vật đã về CoCo trước khi mở bán lại.");
        return await ChangeAsync(id, (order, product, now) =>
        {
            order.ReturnUndelivered(product, now);
            Audit(adminId, order, "DeliveryReturnedUndelivered", now);
            Notify(order.BuyerId, order, $"Đơn {order.ProductSku} không được nhận và đã hủy sau khi hàng về CoCo.", now);
            Notify(order.SellerId, order, $"Đơn {order.ProductSku} không được nhận; món đồ đã về CoCo và mở bán lại.", now);
            return Task.CompletedTask;
        }, ct);
    }
    public async Task<OrderDto> MarkReadyAsync(Guid id, string adminId, CancellationToken ct)
    {
        await EnsureOrderAdminAsync(adminId, ct);
        return await ChangeAsync(id, (order, _, now) =>
        {
            if (lifecycle is null) order.MarkReady(); else lifecycle.MarkReady(order);
            Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã sẵn sàng: {OrderStatusName(order.Status)}.", now);
            Audit(adminId, order, "OrderPrepared", now);
            Notify(order.SellerId, order, $"Đơn {order.ProductSku} chuyển sang {OrderStatusName(order.Status)}.", now);
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
        return await ChangeAsync(id, async (order, product, now) =>
        {
            if (lifecycle is null) order.Complete(product, now);
            else lifecycle.Complete(order, product, now);
            if (payouts is null) throw new InvalidOperationException("Chưa cấu hình dịch vụ ghi có ví người bán.");
            await payouts.CreditCompletedOrderAsync(order, adminId, now, ct);
            Audit(adminId, order, "OrderCompleted", now);
            Notify(order.BuyerId, order, $"Đơn {order.ProductSku} đã giao/nhận thành công.", now);
            Notify(order.SellerId, order, $"{order.ProductSku} đã giao/nhận thành công; tiền bán hàng đã được ghi có vào ví.", now);
        }, ct);
    }
    private Task EnsureOrderAdminAsync(string actorId, CancellationToken ct) =>
        permissions?.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct) ??
        throw new InvalidOperationException("Chưa cấu hình kiểm tra quyền quản lý đơn hàng.");
    private Task<OrderDto> ChangeAsync(Guid id, Func<Order, Product, DateTime, Task> change, CancellationToken ct) =>
        repository.InTransactionAsync(async () =>
        {
            var order = await repository.OrderAsync(id, ct) ?? throw new KeyNotFoundException("Không tìm thấy đơn hàng.");
            var product = await repository.ProductAsync(order.ProductId, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
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
    private static string FulfillmentName(FulfillmentType value) =>
        value == FulfillmentType.Pickup ? "nhận tại CoCo" : "giao tận nơi";
    private static string OrderStatusName(OrderStatus value) => value switch
    {
        OrderStatus.PendingProcessing => "chờ xử lý",
        OrderStatus.ReadyForPickup => "sẵn sàng nhận tại CoCo",
        OrderStatus.Shipping => "đang giao hàng",
        OrderStatus.Completed => "hoàn tất",
        OrderStatus.Cancelled => "đã hủy",
        _ => "đã cập nhật"
    };
    private static OrderDto ToDto(Order x) => new(x.Id, x.ProductId, x.OfferId, x.ProductSku, x.ProductName,
        x.ProductSize, x.BuyerId, x.SellerId, x.IdempotencyKey, x.Fulfillment, x.RecipientName, x.RecipientPhone,
        x.Province, x.Ward, x.StreetAddress, x.PickupAtUtc, x.PickupAddress, x.ItemAmount, x.ShippingFee, x.TotalAmount,
        x.CommissionAmount, x.Status, x.PaymentStatus, x.PaymentReference,
        x.CreatedAtUtc, x.PaidAtUtc, x.CancelledAtUtc, x.CompletedAtUtc,
        x.PreferredDeliveryWindow, x.DeliveryNote, x.PickupDeadlineAtUtc);
    private static OrderDto ForViewer(Order x, string viewerId, bool isAdmin)
    {
        var dto = ToDto(x);
        return isAdmin || x.BuyerId == viewerId ? dto : dto with
        {
            BuyerId = "", IdempotencyKey = Guid.Empty, RecipientName = "CoCo phụ trách", RecipientPhone = "",
            Province = null, Ward = null, StreetAddress = null, PickupAtUtc = null,
            PickupAddress = null, PickupDeadlineAtUtc = null, PaymentReference = null
        };
    }
}
