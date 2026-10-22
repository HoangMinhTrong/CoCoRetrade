using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/listings")]
public sealed class ListingsController(IProductService products) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ProductCard>>> Mine(CancellationToken ct) =>
        Ok(await products.ClosetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetails>> Details(Guid id, CancellationToken ct)
    {
        var product = await products.VisibleDetailsAsync(id,
            User.FindFirstValue(ClaimTypes.NameIdentifier), false, ct);
        return product is null ? NotFound() : Ok(product);
    }
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(55_000_000)]
    public async Task<ActionResult<ProductDetails>> Submit([FromForm] ListingForm form, CancellationToken ct)
    {
        try
        {
            var result = await products.SubmitAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, form.ToRequest(), ct);
            return CreatedAtAction(nameof(Mine), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
