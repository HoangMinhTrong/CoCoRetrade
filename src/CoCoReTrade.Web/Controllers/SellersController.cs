using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class SellersController(IProductService products, ISocialService social,
    IUserProfileService profiles) : Controller
{
    [HttpGet("/sellers/{id}")]
    public async Task<IActionResult> Index(string id, CancellationToken ct)
    {
        var name = await social.SellerNameAsync(id, ct);
        if (name is null) return NotFound();
        ViewBag.SellerId = id;
        ViewBag.SellerName = name;
        ViewBag.SellerProfile = await profiles.GetAsync(id, ct);
        var viewer = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.IsFollowing = viewer is not null &&
            (await social.FollowingIdsAsync(viewer, ct)).Contains(id);
        return View(await products.ShopAsync(new ProductFilter(SellerId: id), ct));
    }
}
