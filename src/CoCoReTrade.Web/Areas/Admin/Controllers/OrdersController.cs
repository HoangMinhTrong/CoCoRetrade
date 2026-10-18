using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = StaffPermissions.ManageOrders)]
public sealed class OrdersController(IAdminOrderService orders) : Controller
{
    private string AdminId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await orders.QueueAsync(AdminId, ct));
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await orders.DetailsAsync(id, AdminId, ct);
        return order is null ? NotFound() : View(order);
    }
    [HttpPost] public Task<IActionResult> Ready(Guid id, CancellationToken ct) =>
        Change(id, () => orders.MarkReadyAsync(id, AdminId, ct));
    [HttpPost] public Task<IActionResult> ConfirmPayment(Guid id, string reference, CancellationToken ct) =>
        Change(id, () => orders.ConfirmPaymentAsync(id, AdminId, reference, ct));
    [HttpPost] public Task<IActionResult> Complete(Guid id, CancellationToken ct) =>
        Change(id, () => orders.CompleteAsync(id, AdminId, ct));
    [HttpPost] public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        Change(id, () => orders.CancelAsync(id, AdminId, ct));
    [HttpPost] public Task<IActionResult> ReturnUndelivered(Guid id, bool itemReturnedToStore, CancellationToken ct) =>
        Change(id, () => orders.ReturnUndeliveredAsync(id, AdminId, itemReturnedToStore, ct));
    private async Task<IActionResult> Change(Guid id, Func<Task<OrderDto>> action)
    {
        try { await action(); TempData["Message"] = "Đã cập nhật đơn hàng."; }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException or InvalidOperationException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
