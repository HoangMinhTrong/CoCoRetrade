using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController, Authorize, Route("api/v1/cart")]
public sealed class CartController(ICartService cart) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductCard>>> Index(CancellationToken ct) =>
        Ok(await cart.ItemsAsync(UserId, ct));

    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> Add(Guid productId, CancellationToken ct)
    {
        try { await cart.AddAsync(UserId, productId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct)
    { await cart.RemoveAsync(UserId, productId, ct); return NoContent(); }
}
