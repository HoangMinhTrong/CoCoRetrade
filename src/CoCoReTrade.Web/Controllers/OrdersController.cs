using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class OrdersController(IOrderService orders) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await orders.MineAsync(UserId, ct));
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await orders.DetailsAsync(id, UserId, User.IsInRole("Admin"), ct);
        return order is null ? NotFound() : View(order);
    }
    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try { await orders.CancelAsync(id, UserId, User.IsInRole("Admin"), ct); TempData["Message"] = "Đã hủy đơn và mở bán lại sản phẩm."; }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
