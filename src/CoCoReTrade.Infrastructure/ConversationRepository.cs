using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class ConversationRepository(AppDbContext db) : IConversationRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }

    public Task<Product?> ProductAsync(Guid id, CancellationToken ct) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Conversation?> ByProductBuyerAsync(Guid productId, string buyerId, CancellationToken ct) =>
        db.Conversations.FirstOrDefaultAsync(x => x.ProductId == productId && x.BuyerId == buyerId, ct);
    public Task<Conversation?> ByIdAsync(Guid id, CancellationToken ct) =>
        db.Conversations.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<ConversationMessage?> LastFromAsync(Guid id, string senderId, CancellationToken ct) =>
        db.ConversationMessages.AsNoTracking().Where(x => x.ConversationId == id && x.SenderId == senderId)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ConversationMessage>> MessagesAsync(Guid id, CancellationToken ct)
    {
        var messages = await db.ConversationMessages.AsNoTracking().Where(x => x.ConversationId == id)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
        messages.Reverse();
        return messages;
    }
    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(string userId, CancellationToken ct) =>
        await QuerySummaries(userId).OrderByDescending(x => x.LastMessageAtUtc).Take(100).ToListAsync(ct);
    public Task<ConversationSummary?> SummaryAsync(Guid id, string userId, CancellationToken ct) =>
        QuerySummaries(userId).FirstOrDefaultAsync(x => x.Id == id, ct);
    private IQueryable<ConversationSummary> QuerySummaries(string userId) =>
        from conversation in db.Conversations.AsNoTracking()
        join buyer in db.Users.AsNoTracking() on conversation.BuyerId equals buyer.Id
        join seller in db.Users.AsNoTracking() on conversation.SellerId equals seller.Id
        where conversation.BuyerId == userId || conversation.SellerId == userId
        select new ConversationSummary(conversation.Id, conversation.ProductId,
            conversation.ProductSku, conversation.ProductName,
            conversation.BuyerId == userId ? seller.FullName : buyer.FullName,
            conversation.LastMessageAtUtc,
            db.ConversationMessages.Count(message => message.ConversationId == conversation.Id &&
                message.SenderId != userId && message.ReadAtUtc == null));

    public Task MarkReadAsync(Guid conversationId, string readerId, DateTime nowUtc, CancellationToken ct) =>
        db.ConversationMessages.Where(x => x.ConversationId == conversationId && x.SenderId != readerId && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.ReadAtUtc, nowUtc), ct);
    public void Add(Conversation conversation) => db.Conversations.Add(conversation);
    public void AddMessage(ConversationMessage message) => db.ConversationMessages.Add(message);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
