using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public interface INotificationRepository
{
    Task AddAsync(MarketplaceNotification notification, CancellationToken ct);
}

public interface INotificationService
{
    Task<Guid> NotifyAsync(string userId, string category, string message,
        Guid? offerId, Guid? orderId, CancellationToken ct);
}

public sealed class NotificationService(INotificationRepository repository, TimeProvider clock) : INotificationService
{
    public async Task<Guid> NotifyAsync(string userId, string category, string message,
        Guid? offerId, Guid? orderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Cần nhập nội dung thông báo.");
        var notification = new MarketplaceNotification
        {
            Id = Guid.NewGuid(), UserId = userId, Category = category.Trim(), Message = message.Trim(),
            OfferId = offerId, OrderId = orderId, CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        await repository.AddAsync(notification, ct);
        return notification.Id;
    }
}

public interface IEmailNotificationProvider
{
    Task SendAsync(Guid messageId, string recipientEmail, string category, string message, CancellationToken ct);
}

public interface IWebPushNotificationProvider
{
    Task SendAsync(Guid messageId, string userId, string category, string message, CancellationToken ct);
}

public sealed record OutboxDelivery(Guid Id, Guid LeaseToken, string Channel,
    string UserId, string? RecipientEmail, string Category, string Message, int AttemptCount);

public interface IOutboxRepository
{
    Task<OutboxDelivery?> ClaimNextAsync(DateTime nowUtc, CancellationToken ct);
    Task MarkSentAsync(Guid id, Guid leaseToken, DateTime nowUtc, CancellationToken ct);
    Task DeferAsync(Guid id, Guid leaseToken, DateTime nextAttemptUtc, string? error, CancellationToken ct);
}

public interface IOutboxDispatcher
{
    Task<int> DispatchAsync(int batchSize, CancellationToken ct);
}

