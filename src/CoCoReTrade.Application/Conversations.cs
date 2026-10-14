using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record ConversationSummary(Guid Id, Guid ProductId, string ProductSku,
    string ProductName, string OtherUserName, DateTime LastMessageAtUtc, int UnreadCount);
public sealed record ConversationMessageDto(Guid Id, string SenderId, string Body,
    DateTime CreatedAtUtc, bool IsRead);
public sealed record ConversationDetails(ConversationSummary Summary,
    IReadOnlyList<ConversationMessageDto> Messages);

public interface IConversationRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);
    Task<Product?> ProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<Conversation?> ByProductBuyerAsync(Guid productId, string buyerId, CancellationToken cancellationToken);
    Task<Conversation?> ByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IsParticipantAsync(Guid conversationId, string userId, CancellationToken cancellationToken);
    Task<ConversationMessage?> LastFromAsync(Guid conversationId, string senderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationMessage>> MessagesAsync(Guid conversationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationSummary>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<ConversationSummary?> SummaryAsync(Guid conversationId, string userId, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid conversationId, string readerId, DateTime nowUtc, CancellationToken cancellationToken);
    void Add(Conversation conversation);
    void AddMessage(ConversationMessage message);
    void AddNotification(MarketplaceNotification notification);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IConversationService
{
    Task<Guid> OpenAsync(Guid productId, string buyerId, CancellationToken cancellationToken);
    Task<bool> CanAccessAsync(Guid conversationId, string userId, CancellationToken cancellationToken);
    Task<ConversationMessageDto> SendAsync(Guid conversationId, string actorId, string body,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationSummary>> ListAsync(string userId, CancellationToken cancellationToken);
    Task<ConversationDetails> DetailsAsync(Guid conversationId, string userId, CancellationToken cancellationToken);
}

public interface IConversationRealtimePublisher
{
    Task PublishAsync(Guid conversationId, string recipientId, ConversationMessageDto message,
        CancellationToken cancellationToken);
}

public sealed class ConversationService(IConversationRepository repository, TimeProvider clock,
    IConversationRealtimePublisher realtime) : IConversationService
{
    public Task<bool> CanAccessAsync(Guid conversationId, string userId, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(userId) ? Task.FromResult(false) :
            repository.IsParticipantAsync(conversationId, userId, ct);

    public Task<Guid> OpenAsync(Guid productId, string buyerId, CancellationToken ct)
    {
        RequireUser(buyerId);
        return repository.InTransactionAsync(async () =>
        {
            var product = await repository.ProductAsync(productId, ct);
            if (product is null || product.Status != ProductStatus.Active)
                throw new KeyNotFoundException("Sản phẩm không còn mở bán.");
            if (product.SellerId == buyerId)
                throw new InvalidOperationException("Không thể tự nhắn tin về sản phẩm của mình.");
            var existing = await repository.ByProductBuyerAsync(productId, buyerId, ct);
            if (existing is not null) return existing.Id;
            var now = clock.GetUtcNow().UtcDateTime;
            var conversation = new Conversation
            {
                ProductId = productId, ProductSku = product.Sku, ProductName = product.Name,
                BuyerId = buyerId, SellerId = product.SellerId,
                CreatedAtUtc = now, LastMessageAtUtc = now
            };
            repository.Add(conversation);
            await repository.SaveAsync(ct);
            return conversation.Id;
        }, ct);
    }

    public async Task<ConversationMessageDto> SendAsync(Guid conversationId, string actorId, string body, CancellationToken ct)
    {
        RequireUser(actorId);
        var message = body?.Trim() ?? string.Empty;
        if (message.Length is < 1 or > 1000)
            throw new ArgumentException("Tin nhắn phải có từ 1 đến 1000 ký tự.");
        var saved = await repository.InTransactionAsync(async () =>
        {
            var conversation = await repository.ByIdAsync(conversationId, ct) ??
                throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");
            if (conversation.BuyerId != actorId && conversation.SellerId != actorId)
                throw new UnauthorizedAccessException("Không có quyền gửi tin nhắn trong cuộc trò chuyện này.");
            var now = clock.GetUtcNow().UtcDateTime;
            var last = await repository.LastFromAsync(conversationId, actorId, ct);
            if (last is not null && last.CreatedAtUtc > now.AddSeconds(-3))
                throw new InvalidOperationException("Vui lòng chờ vài giây trước khi gửi tiếp.");
            var item = new ConversationMessage
            {
                ConversationId = conversation.Id, SenderId = actorId,
                Body = message, CreatedAtUtc = now
            };
            conversation.LastMessageAtUtc = now;
            repository.AddMessage(item);
            repository.AddNotification(new MarketplaceNotification
            {
                Id = Guid.NewGuid(), UserId = actorId == conversation.BuyerId ? conversation.SellerId : conversation.BuyerId,
                Category = "Messages", Message = $"Bạn có tin nhắn mới về {conversation.ProductSku}.",
                CreatedAtUtc = now
            });
            await repository.SaveAsync(ct);
            return (Message: new ConversationMessageDto(item.Id, item.SenderId, item.Body, item.CreatedAtUtc, false),
                RecipientId: actorId == conversation.BuyerId ? conversation.SellerId : conversation.BuyerId);
        }, ct);
        // Broadcast only after the SQL transaction commits. Delivery is best-effort; SQL remains authoritative.
        await realtime.PublishAsync(conversationId, saved.RecipientId, saved.Message, CancellationToken.None);
        return saved.Message;
    }

    public Task<IReadOnlyList<ConversationSummary>> ListAsync(string userId, CancellationToken ct)
    { RequireUser(userId); return repository.ListAsync(userId, ct); }

    public async Task<ConversationDetails> DetailsAsync(Guid conversationId, string userId, CancellationToken ct)
    {
        RequireUser(userId);
        var conversation = await repository.ByIdAsync(conversationId, ct) ??
            throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");
        if (conversation.BuyerId != userId && conversation.SellerId != userId)
            throw new UnauthorizedAccessException("Không có quyền xem cuộc trò chuyện này.");
        await repository.MarkReadAsync(conversationId, userId, clock.GetUtcNow().UtcDateTime, ct);
        var summary = await repository.SummaryAsync(conversationId, userId, ct) ??
            throw new KeyNotFoundException("Cuộc trò chuyện không tồn tại.");
        var messages = await repository.MessagesAsync(conversationId, ct);
        return new ConversationDetails(summary, messages.Select(x =>
            new ConversationMessageDto(x.Id, x.SenderId, x.Body, x.CreatedAtUtc, x.ReadAtUtc.HasValue)).ToList());
    }

    private static void RequireUser(string userId)
    { if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập."); }
}
