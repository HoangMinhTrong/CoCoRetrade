using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record SendMessageRequest(string Body);

[ApiController, Authorize, Route("api/v1/messages")]
public sealed class MessagesApiController(IConversationService conversations) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationSummary>>> Index(CancellationToken ct) =>
        Ok(await conversations.ListAsync(UserId, ct));

    [HttpPost("products/{productId:guid}")]
    public async Task<IActionResult> Start(Guid productId, CancellationToken ct)
    {
        try
        {
            var id = await conversations.OpenAsync(productId, UserId, ct);
            return Ok(new { id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationDetails>> Details(Guid id, CancellationToken ct)
    {
        try { return Ok(await conversations.DetailsAsync(id, UserId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<ConversationMessageDto>> Send(Guid id, SendMessageRequest request, CancellationToken ct)
    {
        try { return Ok(await conversations.SendAsync(id, UserId, request.Body, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return BadRequest(new { error = ex.Message }); }
    }
}
