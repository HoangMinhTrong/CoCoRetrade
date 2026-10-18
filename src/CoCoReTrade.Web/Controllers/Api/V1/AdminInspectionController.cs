using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record RejectListingRequest(string Reason, string ExpectedRowVersion);
public sealed record PublishListingRequest(string ExpectedRowVersion, bool PhysicalItemConfirmed);
public sealed record SelectCoverRequest(Guid ImageId, string ExpectedRowVersion);

[ApiController]
[Authorize]
[Route("api/v1/admin/inspections")]
public sealed class AdminInspectionController(IAdminProductService products) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("pending"), Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<ActionResult<IReadOnlyList<AdminProductItem>>> Pending([FromQuery] string? search, CancellationToken ct) =>
        Ok(await products.PendingAsync(ActorId, search, ct));

    [HttpGet("{id:guid}"), Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<ActionResult<AdminProductDetails>> GetOne(Guid id, CancellationToken ct)
    {
        var product = await products.GetAsync(ActorId, id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("{id:guid}"), Authorize(Policy = StaffPermissions.Inspect)]
    [Consumes("multipart/form-data"), RequestSizeLimit(55_000_000)]
    public Task<ActionResult<AdminProductDetails>> Record(Guid id, [FromForm] InspectionForm request, CancellationToken ct) =>
        Execute(() => products.InspectAsync(ActorId, id, request.ToRequest(), ct));

    [HttpPost("{id:guid}/cover"), Authorize(Policy = StaffPermissions.Inspect)]
    public Task<ActionResult<AdminProductDetails>> Cover(Guid id, [FromBody] SelectCoverRequest request, CancellationToken ct) =>
        Execute(() => products.SelectCoverAsync(ActorId, id, request.ImageId, request.ExpectedRowVersion, ct));

    [HttpPost("{id:guid}/publish"), Authorize(Policy = StaffPermissions.Publish)]
    public Task<ActionResult<AdminProductDetails>> Publish(Guid id, [FromBody] PublishListingRequest request, CancellationToken ct) =>
        Execute(() => products.PublishAsync(ActorId, id, request.ExpectedRowVersion,
            request.PhysicalItemConfirmed, ct));

    [HttpPost("{id:guid}/reject"), Authorize(Policy = StaffPermissions.Reject)]
    public Task<ActionResult<AdminProductDetails>> Reject(Guid id, [FromBody] RejectListingRequest request, CancellationToken ct) =>
        Execute(() => products.RejectAsync(ActorId, id, request.Reason, request.ExpectedRowVersion, ct));

    private async Task<ActionResult<AdminProductDetails>> Execute(Func<Task<AdminProductDetails>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateConcurrencyException)
        { return Conflict(new { error = ex.Message }); }
    }
}
