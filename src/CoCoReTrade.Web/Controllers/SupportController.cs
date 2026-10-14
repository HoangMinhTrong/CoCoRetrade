using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class SupportController(ISupportService support) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await support.MineAsync(UserId, ct));
    [HttpGet]
    public IActionResult Create() => View();
    [HttpPost]
    public async Task<IActionResult> Create(string subject, string body, CancellationToken ct)
    {
        try
        {
            var ticket = await support.CreateAsync(UserId, subject, body, ct);
            TempData["Message"] = "Đã gửi yêu cầu tới CoCo.";
            return RedirectToAction(nameof(Details), new { id = ticket.Ticket.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Subject = subject;
            ViewBag.Body = body;
            return View();
        }
    }
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        try { return View(await support.DetailsAsync(id, UserId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    [HttpPost]
    public async Task<IActionResult> Reply(Guid id, string body, CancellationToken ct)
    {
        try { await support.ReplyAsync(id, UserId, body, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
