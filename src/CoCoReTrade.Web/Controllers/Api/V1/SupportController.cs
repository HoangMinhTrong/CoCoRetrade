using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record CreateSupportRequest(string Subject, string Body);
public sealed record ReplySupportRequest(string Body);

[ApiController, Authorize, Route("api/v1/support")]
public sealed class SupportApiController(ISupportService support) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupportTicketRow>>> Mine(CancellationToken ct) =>
        Ok(await support.MineAsync(UserId, ct));
    [HttpPost]
    public async Task<ActionResult<SupportTicketDetails>> Create(CreateSupportRequest request, CancellationToken ct)
    {
        try { return Ok(await support.CreateAsync(UserId, request.Subject, request.Body, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupportTicketDetails>> Details(Guid id, CancellationToken ct)
    {
        try { return Ok(await support.DetailsAsync(id, UserId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<SupportTicketDetails>> Reply(Guid id, ReplySupportRequest request, CancellationToken ct)
    {
        try { return Ok(await support.ReplyAsync(id, UserId, request.Body, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return BadRequest(new { error = ex.Message }); }
    }
}
