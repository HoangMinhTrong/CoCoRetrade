using CoCoReTrade.Application;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class ShopController(IProductService products, ISocialService social) : Controller
{
    public async Task<IActionResult> Index([FromQuery] ProductFilter filter, CancellationToken ct)
    {
        ViewBag.Filter = filter;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        ViewBag.FavoriteIds = userId is null ? Array.Empty<Guid>() : await social.FavoriteIdsAsync(userId, ct);
        return View(await products.ShopAsync(filter, ct));
    }
    [HttpGet("/Shop/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var result = await products.VisibleDetailsAsync(id, User.Identity?.IsAuthenticated == true ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value : null,
            User.IsInRole("Admin") && User.HasClaim(StaffPermissions.ClaimType, StaffPermissions.ManageProducts), ct);
        if (result is null) return NotFound();
        ViewBag.SellerName = await social.SellerNameAsync(result.SellerId, ct);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        ViewBag.IsFavorite = userId is not null && (await social.FavoriteIdsAsync(userId, ct)).Contains(result.Id);
        ViewBag.IsFollowing = userId is not null && (await social.FollowingIdsAsync(userId, ct)).Contains(result.SellerId);
        return View(result);
    }
}
