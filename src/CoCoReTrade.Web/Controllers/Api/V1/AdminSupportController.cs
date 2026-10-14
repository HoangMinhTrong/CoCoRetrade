using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController, Authorize(Policy = StaffPermissions.ManageUsers), Route("api/v1/admin/support")]
public sealed class AdminSupportController(ISupportService support) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportTicketRow>>> Queue(CancellationToken ct) =>
        Ok(await support.AdminQueueAsync(ActorId, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupportTicketDetails>> Details(Guid id, CancellationToken ct)
    {
        try { return Ok(await support.DetailsAsync(id, ActorId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<SupportTicketDetails>> Reply(Guid id, ReplySupportRequest request, CancellationToken ct)
    {
        try { return Ok(await support.ReplyAsync(id, ActorId, request.Body, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return BadRequest(new { error = ex.Message }); }
    }
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<SupportTicketDetails>> Close(Guid id, CancellationToken ct)
    {
        try { return Ok(await support.CloseAsync(id, ActorId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
