using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoCoReTrade.Web.Realtime;

[Authorize]
public sealed class ConversationHub(IConversationService conversations) : Hub
{
    public static string GroupName(Guid conversationId) => $"conversation:{conversationId:N}";
    public static string UserGroupName(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is { Length: > 0 } userId)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroupName(userId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = Context.UserIdentifier;
        if (conversationId == Guid.Empty || userId is null ||
            !await conversations.CanAccessAsync(conversationId, userId, Context.ConnectionAborted))
            throw new HubException("Bạn không có quyền xem cuộc trò chuyện này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId),
            Context.ConnectionAborted);
    }
}
