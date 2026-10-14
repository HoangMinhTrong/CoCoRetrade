using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationCenter notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Mine(string? group, CancellationToken ct)
    {
        try { return Ok(await notifications.ListAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, group, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        try { await notifications.MarkReadAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
