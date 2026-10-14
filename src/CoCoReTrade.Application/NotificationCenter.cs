using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public interface INotificationCenterRepository
{
    Task<IReadOnlyList<MarketplaceNotification>> ForUserAsync(string userId, CancellationToken ct);
    Task<MarketplaceNotification?> OwnedAsync(string userId, Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public interface INotificationCenter
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(string userId, string? group, CancellationToken ct);
    Task MarkReadAsync(string userId, Guid id, CancellationToken ct);
}

public sealed class NotificationCenter(INotificationCenterRepository repository) : INotificationCenter
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(string userId, string? group, CancellationToken ct)
    {
        RequireUser(userId);
        if (group is not null && group is not ("transactions" or "listings" or "social"))
            throw new ArgumentException("Nhóm thông báo không hợp lệ.");
        var notifications = await repository.ForUserAsync(userId, ct);
        return notifications.Where(x => group is null || Group(x.Category) == group)
            .Select(x => new NotificationDto(x.Id, x.Category, x.Message,
                x.OfferId, x.OrderId, x.IsRead, x.CreatedAtUtc)).ToList();
    }
    public async Task MarkReadAsync(string userId, Guid id, CancellationToken ct)
    {
        RequireUser(userId);
        var notification = await repository.OwnedAsync(userId, id, ct) ??
            throw new KeyNotFoundException("Thông báo không tồn tại.");
        if (notification.IsRead) return;
        notification.IsRead = true;
        await repository.SaveAsync(ct);
    }
    private static string Group(string category) => category.ToLowerInvariant() switch
    {
        "orders" or "offers" or "wallets" => "transactions",
        "listings" => "listings",
        _ => "social"
    };
    private static void RequireUser(string userId)
    { if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập."); }
}
