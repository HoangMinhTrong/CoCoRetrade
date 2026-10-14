using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoCoReTrade.Web.Realtime;

[Authorize]
public sealed class SupportHub(ISupportService support) : Hub
{
    public static string GroupName(Guid ticketId) => $"support:{ticketId:N}";

    public async Task JoinTicket(Guid ticketId)
    {
        var actorId = Context.UserIdentifier;
        if (actorId is null || !await support.CanAccessAsync(ticketId, actorId, Context.ConnectionAborted))
            throw new HubException("Bạn không có quyền xem yêu cầu hỗ trợ này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(ticketId), Context.ConnectionAborted);
    }
}
