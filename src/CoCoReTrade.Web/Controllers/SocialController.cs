using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class SocialController(ISocialService social) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpPost]
    public async Task<IActionResult> Favorite(Guid productId, bool selected, string? returnUrl, CancellationToken ct)
    {
        try { await social.SetFavoriteAsync(UserId, productId, selected, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Shop")!);
    }
    [HttpPost]
    public async Task<IActionResult> Follow(string sellerId, bool selected, string? returnUrl, CancellationToken ct)
    {
        try { await social.SetFollowAsync(UserId, sellerId, selected, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }
}
