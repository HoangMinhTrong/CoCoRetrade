namespace CoCoReTrade.Application;

public sealed record UnreadCountsDto(int Notifications, int Messages);

public interface IUnreadCountsRepository
{
    Task<int> UnreadNotificationsAsync(string userId, CancellationToken ct);
    Task<int> UnreadMessagesAsync(string userId, CancellationToken ct);
}

public interface IUnreadCountsService
{
    Task<UnreadCountsDto> GetAsync(string userId, CancellationToken ct);
}

public sealed class UnreadCountsService(IUnreadCountsRepository repository) : IUnreadCountsService
{
    public async Task<UnreadCountsDto> GetAsync(string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập.");
        var notifications = await repository.UnreadNotificationsAsync(userId, ct);
        var messages = await repository.UnreadMessagesAsync(userId, ct);
        return new UnreadCountsDto(notifications, messages);
    }
}
