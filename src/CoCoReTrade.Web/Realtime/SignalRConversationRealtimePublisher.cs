using CoCoReTrade.Application;
using Microsoft.AspNetCore.SignalR;

namespace CoCoReTrade.Web.Realtime;

public sealed class SignalRConversationRealtimePublisher(
    IHubContext<ConversationHub> hub, ILogger<SignalRConversationRealtimePublisher> logger)
    : IConversationRealtimePublisher
{
    public async Task PublishAsync(Guid conversationId, string recipientId,
        ConversationMessageDto message, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Group(ConversationHub.GroupName(conversationId))
                .SendAsync("MessageReceived", message, ct);
            await hub.Clients.Group(ConversationHub.UserGroupName(recipientId))
                .SendAsync("UnreadCountsChanged", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể phát tin nhắn {MessageId} cho hội thoại {ConversationId}",
                message.Id, conversationId);
        }
    }
}
