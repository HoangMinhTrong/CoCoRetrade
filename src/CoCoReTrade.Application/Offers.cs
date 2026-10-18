using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed class MarketplacePolicy
{
    public int MaximumOfferDiscountPercent { get; }
    public int CommissionPercent { get; }
    public MarketplacePolicy(int maximumOfferDiscountPercent = 25, int commissionPercent = 15)
    {
        if (maximumOfferDiscountPercent is < 20 or > 30) throw new ArgumentOutOfRangeException(nameof(maximumOfferDiscountPercent), "Mức giảm tối đa phải từ 20% đến 30%.");
        if (commissionPercent is < 15 or > 20) throw new ArgumentOutOfRangeException(nameof(commissionPercent), "Hoa hồng phải từ 15% đến 20%.");
        MaximumOfferDiscountPercent = maximumOfferDiscountPercent;
        CommissionPercent = commissionPercent;
    }
    public decimal MinimumOffer(decimal askingPrice) => decimal.Ceiling(askingPrice * (100 - MaximumOfferDiscountPercent) / 100);
}

public enum OfferResponse { Accept, Decline, Counter }
public sealed record OfferEventDto(OfferEventAction Action, string ActorId, decimal? Price, DateTime CreatedAtUtc);
public sealed record OfferDto(Guid Id, Guid ProductId, string ProductSku, string ProductName, string BuyerId, string SellerId,
    decimal AskingPrice, decimal CurrentPrice, decimal? AcceptedPrice, OfferStatus Status, int BuyerOffersUsed,
    DateTime CreatedAtUtc, DateTime ExpiresAtUtc, IReadOnlyList<OfferEventDto> Events,
    decimal? CounterMinimumPrice = null, decimal? CounterMaximumPrice = null);
public sealed record NotificationDto(Guid Id, string Category, string Message, Guid? OfferId, Guid? OrderId,
    bool IsRead, DateTime CreatedAtUtc, Guid? ConversationId = null, Guid? ProductId = null)
{
    public bool HasDestination => OfferId.HasValue || OrderId.HasValue || ConversationId.HasValue ||
        ProductId.HasValue || Category.Equals("Cart", StringComparison.OrdinalIgnoreCase) ||
        Category.Equals("Carts", StringComparison.OrdinalIgnoreCase) ||
        Category.Equals("Messages", StringComparison.OrdinalIgnoreCase) ||
        Category.Equals("Offers", StringComparison.OrdinalIgnoreCase) ||
        Category.Equals("Orders", StringComparison.OrdinalIgnoreCase) ||
        Category.Equals("Wallets", StringComparison.OrdinalIgnoreCase) ||
        Category.Equals("Support", StringComparison.OrdinalIgnoreCase);
}

public interface IOfferRepository
{
    Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct);
    Task<Product?> ProductAsync(Guid id, CancellationToken ct);
    Task<Offer?> OfferAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Offer>> ForUserAsync(string userId, CancellationToken ct);
    Task<int> BuyerPriceCountAsync(Guid productId, string buyerId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueForExpirationAsync(DateTime nowUtc, int batchSize, CancellationToken ct);
    Task AddOfferAsync(Offer offer, CancellationToken ct);
    void AddEvent(OfferEvent offerEvent);
    void AddNotification(MarketplaceNotification notification);
    Task<IReadOnlyList<MarketplaceNotification>> NotificationsAsync(string userId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public interface IOfferService
{
    Task<OfferDto> MakeAsync(Guid productId, string buyerId, decimal price, CancellationToken ct);
    Task<OfferDto> RespondAsync(Guid offerId, string actorId, OfferResponse action, decimal? price, CancellationToken ct);
    Task<OfferDto?> DetailsAsync(Guid offerId, string userId, CancellationToken ct);
    Task<IReadOnlyList<OfferDto>> MineAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<NotificationDto>> NotificationsAsync(string userId, CancellationToken ct);
}

public sealed class OfferService(IOfferRepository repository, MarketplacePolicy policy, TimeProvider clock) : IOfferService
{
    public Task<OfferDto> MakeAsync(Guid productId, string buyerId, decimal price, CancellationToken ct) =>
        repository.InSerializableTransactionAsync(async () =>
        {
            var product = await repository.ProductAsync(productId, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            var used = await repository.BuyerPriceCountAsync(productId, buyerId, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var offer = Offer.Create(product, buyerId, price, policy.MinimumOffer(product.AskingPrice), used, now);
            await repository.AddOfferAsync(offer, ct);
            Notify(product.SellerId, offer.Id, $"Có giá đề xuất {price:N0} VNĐ cho {product.Sku}.", now);
            await repository.SaveAsync(ct);
            return ToDto(offer, used + 1, now);
        }, ct);

    public Task<OfferDto> RespondAsync(Guid offerId, string actorId, OfferResponse action, decimal? price, CancellationToken ct) =>
        repository.InSerializableTransactionAsync(async () =>
        {
            var offer = await repository.OfferAsync(offerId, ct) ?? throw new KeyNotFoundException("Không tìm thấy đề xuất giá.");
            var product = await repository.ProductAsync(offer.ProductId, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            var now = clock.GetUtcNow().UtcDateTime;
            var used = await repository.BuyerPriceCountAsync(product.Id, offer.BuyerId, ct);
            switch (action)
            {
                case OfferResponse.Accept: offer.Accept(actorId, product.Status, now); break;
                case OfferResponse.Decline: offer.Decline(actorId, product.Status, now); break;
                case OfferResponse.Counter:
                    if (price is null) throw new ArgumentException("Cần nhập mức giá đề xuất lại.");
                    offer.Counter(actorId, price.Value, policy.MinimumOffer(product.AskingPrice), used, product.Status, now);
                    if (actorId == offer.BuyerId) used++;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
            var other = actorId == offer.BuyerId ? offer.SellerId : offer.BuyerId;
            repository.AddEvent(offer.Events[^1]);
            var message = action switch
            {
                OfferResponse.Accept => $"Giá {offer.AcceptedPrice:N0} VNĐ cho {offer.ProductSku} đã được chấp nhận. Có hiệu lực 24 giờ.",
                OfferResponse.Decline => $"Đề xuất cho {offer.ProductSku} đã bị từ chối.",
                _ => $"Có giá đề xuất lại {offer.CurrentPrice:N0} VNĐ cho {offer.ProductSku}."
            };
            Notify(other, offer.Id, message, now);
            await repository.SaveAsync(ct);
            return ToDto(offer, used, now);
        }, ct);

    public async Task<OfferDto?> DetailsAsync(Guid offerId, string userId, CancellationToken ct)
    {
        var offer = await repository.OfferAsync(offerId, ct);
        if (offer is null || (offer.BuyerId != userId && offer.SellerId != userId)) return null;
        return ToDto(offer, await repository.BuyerPriceCountAsync(offer.ProductId, offer.BuyerId, ct), clock.GetUtcNow().UtcDateTime);
    }
    public async Task<IReadOnlyList<OfferDto>> MineAsync(string userId, CancellationToken ct)
    {
        var offers = await repository.ForUserAsync(userId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var result = new List<OfferDto>();
        foreach (var offer in offers)
            result.Add(ToDto(offer, await repository.BuyerPriceCountAsync(offer.ProductId, offer.BuyerId, ct), now));
        return result;
    }
    public async Task<IReadOnlyList<NotificationDto>> NotificationsAsync(string userId, CancellationToken ct) =>
        (await repository.NotificationsAsync(userId, ct)).Select(x => new NotificationDto(x.Id, x.Category, x.Message, x.OfferId, x.OrderId, x.IsRead, x.CreatedAtUtc)).ToList();

    private void Notify(string userId, Guid offerId, string message, DateTime nowUtc) =>
        repository.AddNotification(new MarketplaceNotification { Id = Guid.NewGuid(), UserId = userId, OfferId = offerId,
            Category = "offers", Message = message, CreatedAtUtc = nowUtc });
    private OfferDto ToDto(Offer x, int used, DateTime nowUtc)
    {
        var status = x.EffectiveStatus(nowUtc);
        var range = status == x.Status ? x.CounterPriceRange(policy.MinimumOffer(x.AskingPrice), used) : null;
        return new OfferDto(x.Id, x.ProductId, x.ProductSku, x.ProductName,
            x.BuyerId, x.SellerId, x.AskingPrice, x.CurrentPrice, x.AcceptedPrice, status, used,
            x.CreatedAtUtc, x.ExpiresAtUtc, x.Events.OrderBy(e => e.CreatedAtUtc).Select(e =>
                new OfferEventDto(e.Action, e.ActorId, e.Price, e.CreatedAtUtc)).ToList(),
            range?.Minimum, range?.Maximum);
    }
}

public interface IOfferExpirationService
{
    Task<int> ExpireDueAsync(int batchSize, CancellationToken ct);
}

public sealed class OfferExpirationService(IOfferRepository repository, TimeProvider clock) : IOfferExpirationService
{
    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await repository.DueForExpirationAsync(now, batchSize, ct);
        var expired = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var changed = await repository.InSerializableTransactionAsync(async () =>
            {
                var offer = await repository.OfferAsync(id, ct);
                if (offer is null || !offer.Expire(now)) return false;
                repository.AddEvent(offer.Events[^1]);
                foreach (var userId in new[] { offer.BuyerId, offer.SellerId }.Distinct())
                    repository.AddNotification(new MarketplaceNotification
                    {
                        Id = Guid.NewGuid(), UserId = userId, OfferId = offer.Id,
                        Category = "offers", Message = $"Đề xuất cho {offer.ProductSku} đã hết hạn.", CreatedAtUtc = now
                    });
                await repository.SaveAsync(ct);
                return true;
            }, ct);
            if (changed) expired++;
        }
        return expired;
    }
}
