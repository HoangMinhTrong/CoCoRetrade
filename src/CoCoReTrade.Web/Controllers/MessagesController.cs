using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class MessagesController(IConversationService conversations) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await conversations.ListAsync(UserId, ct));

    [HttpPost]
    public async Task<IActionResult> Start(Guid productId, CancellationToken ct)
    {
        try
        {
            var id = await conversations.OpenAsync(productId, UserId, ct);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex)
        { TempData["Error"] = ex.Message; return RedirectToAction("Index", "Shop"); }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try { return View(await conversations.DetailsAsync(id, UserId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    public async Task<IActionResult> Send(Guid id, string body, CancellationToken ct)
    {
        try { await conversations.SendAsync(id, UserId, body, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
