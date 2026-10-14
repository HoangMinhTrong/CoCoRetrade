using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = StaffPermissions.ManageUsers)]
public sealed class UsersController(IAdminUserService users) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        ViewBag.Search = search;
        return View(await users.SearchAsync(ActorId, search, ct));
    }
    [HttpPost]
    public Task<IActionResult> Inspector(string id, bool enabled, CancellationToken ct) =>
        Change(() => users.SetInspectorAsync(ActorId, id, enabled, ct));
    [HttpPost]
    public Task<IActionResult> PublishGrant(string id, bool enabled, CancellationToken ct) =>
        Change(() => users.SetPublishGrantAsync(ActorId, id, enabled, ct));
    [HttpPost]
    public Task<IActionResult> Lock(string id, bool locked, CancellationToken ct) =>
        Change(() => users.SetLockedAsync(ActorId, id, locked, ct));
    [HttpPost]
    public Task<IActionResult> Trust(string id, int score, string reason, CancellationToken ct) =>
        Change(() => users.SetTrustScoreAsync(ActorId, id, score, reason, ct));
    private async Task<IActionResult> Change(Func<Task<AdminUserRow>> action)
    {
        try { await action(); TempData["Message"] = "Đã cập nhật tài khoản."; }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
