using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = StaffPermissions.ManageUsers)]
public sealed class HelpDeskController(ISupportService support) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await support.AdminQueueAsync(ActorId, ct));
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try { return View(await support.DetailsAsync(id, ActorId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost]
    public async Task<IActionResult> Reply(Guid id, string body, CancellationToken ct)
    {
        try { await support.ReplyAsync(id, ActorId, body, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        try { await support.CloseAsync(id, ActorId, ct); TempData["Message"] = "Đã đóng yêu cầu."; }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
