using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController, Authorize, Route("api/v1/addresses")]
public sealed class AddressesController(IAddressBookService addresses) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AddressDto>>> List(CancellationToken ct) =>
        Ok(await addresses.ListAsync(UserId, ct));
    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create([FromBody] AddressInput input, CancellationToken ct)
    {
        if (input.Id.HasValue) return BadRequest(new { error = "Id is assigned by the server." });
        try { return Ok(await addresses.SaveAsync(UserId, input, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AddressDto>> Update(Guid id, [FromBody] AddressInput input, CancellationToken ct)
    {
        try { return Ok(await addresses.SaveAsync(UserId, input with { Id = id }, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await addresses.DeleteAsync(UserId, id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
