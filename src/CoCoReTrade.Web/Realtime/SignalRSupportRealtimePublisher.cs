using CoCoReTrade.Application;
using Microsoft.AspNetCore.SignalR;

namespace CoCoReTrade.Web.Realtime;

public sealed class SignalRSupportRealtimePublisher(
    IHubContext<SupportHub> hub, ILogger<SignalRSupportRealtimePublisher> logger)
    : ISupportRealtimePublisher
{
    public async Task PublishChangedAsync(Guid ticketId, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Group(SupportHub.GroupName(ticketId))
                .SendAsync("SupportTicketChanged", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không thể phát thay đổi của yêu cầu hỗ trợ {TicketId}", ticketId);
        }
    }
}
