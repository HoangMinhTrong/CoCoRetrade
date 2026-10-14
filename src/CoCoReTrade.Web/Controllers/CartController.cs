using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class CartController(ICartService cart) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await cart.ItemsAsync(UserId, ct));

    [HttpPost]
    public async Task<IActionResult> Add(Guid productId, CancellationToken ct)
    {
        try
        {
            await cart.AddAsync(UserId, productId, ct);
            TempData["Message"] = "Đã thêm món đồ vào giỏ. Món đồ vẫn có thể được người khác mua trước khi bạn đặt hàng.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct)
    {
        await cart.RemoveAsync(UserId, productId, ct);
        return RedirectToAction(nameof(Index));
    }
}
