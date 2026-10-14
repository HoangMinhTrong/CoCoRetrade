using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record PriceRequest(decimal Price);

[ApiController]
[Authorize]
[Route("api/v1/offers")]
public sealed class OffersApiController(IOfferService offers) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OfferDto>>> Mine(CancellationToken ct) => Ok(await offers.MineAsync(UserId, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OfferDto>> Details(Guid id, CancellationToken ct)
    {
        var offer = await offers.DetailsAsync(id, UserId, ct);
        return offer is null ? NotFound() : Ok(offer);
    }
    [HttpPost("/api/v1/products/{productId:guid}/offers")]
    public async Task<ActionResult<OfferDto>> Make(Guid productId, [FromBody] PriceRequest request, CancellationToken ct) =>
        await Execute(() => offers.MakeAsync(productId, UserId, request.Price, ct), created: true);
    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<OfferDto>> Accept(Guid id, CancellationToken ct) =>
        await Execute(() => offers.RespondAsync(id, UserId, OfferResponse.Accept, null, ct));
    [HttpPost("{id:guid}/decline")]
    public async Task<ActionResult<OfferDto>> Decline(Guid id, CancellationToken ct) =>
        await Execute(() => offers.RespondAsync(id, UserId, OfferResponse.Decline, null, ct));
    [HttpPost("{id:guid}/counter")]
    public async Task<ActionResult<OfferDto>> Counter(Guid id, [FromBody] PriceRequest request, CancellationToken ct) =>
        await Execute(() => offers.RespondAsync(id, UserId, OfferResponse.Counter, request.Price, ct));
    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action, bool created = false)
    {
        try
        {
            var result = await action();
            return created ? StatusCode(StatusCodes.Status201Created, result) : Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
        { return Conflict(new { error = ex.Message }); }
    }
}
