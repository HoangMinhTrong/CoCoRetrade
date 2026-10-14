using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class ImagesController(IProductService products) : Controller
{
    [HttpGet("/images/{id:guid}")]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await products.VisibleImageAsync(id, User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            (User.IsInRole("Inspector") || User.IsInRole("Admin")) && User.HasClaim(StaffPermissions.ClaimType, StaffPermissions.ReadPending),
            User.IsInRole("Admin") && User.HasClaim(StaffPermissions.ClaimType, StaffPermissions.ManageProducts), ct);
        return result is null ? NotFound() : File(result.Value.Stream, result.Value.ContentType);
    }
}
