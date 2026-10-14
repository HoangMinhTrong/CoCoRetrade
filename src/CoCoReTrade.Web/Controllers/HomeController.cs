using CoCoReTrade.Application;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class HomeController(IProductService products, ISocialService social) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        ViewBag.FavoriteIds = userId is null ? Array.Empty<Guid>() : await social.FavoriteIdsAsync(userId, ct);
        return View(await products.ShopAsync(new ProductFilter(), ct));
    }
    public IActionResult Error() => View();
    public IActionResult Terms() => View();
}
