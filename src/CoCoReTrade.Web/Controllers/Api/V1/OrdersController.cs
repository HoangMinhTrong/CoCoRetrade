using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Mine(CancellationToken ct) => Ok(await orders.MineAsync(UserId, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Details(Guid id, CancellationToken ct)
    {
        var order = await orders.DetailsAsync(id, UserId, User.IsInRole("Admin"), ct);
        return order is null ? NotFound() : Ok(order);
    }
    [HttpGet("quote")]
    public async Task<ActionResult<CheckoutQuote>> Quote([FromQuery] Guid productId, [FromQuery] Guid? offerId,
        [FromQuery] FulfillmentType fulfillment, [FromQuery] string? province, [FromQuery] string? ward, CancellationToken ct)
    {
        try { return Ok(await orders.QuoteAsync(productId, offerId, UserId, fulfillment, province, ward, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        try
        {
            var order = await orders.PlaceAsync(request, UserId, ct);
            return CreatedAtAction(nameof(Details), new { id = order.Id }, order);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken ct)
    {
        try { return Ok(await orders.CancelAsync(id, UserId, User.IsInRole("Admin"), ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
