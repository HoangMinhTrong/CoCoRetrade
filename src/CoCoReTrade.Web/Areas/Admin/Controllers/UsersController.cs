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
    public Task<IActionResult> Inspector(string id, bool? enabled, CancellationToken ct) =>
        ChangeFlag(enabled, value => users.SetInspectorAsync(ActorId, id, value, ct),
            enabled == true ? "Đã cấp quyền kiểm định." : "Đã thu hồi quyền kiểm định.");
    [HttpPost]
    public Task<IActionResult> PublishGrant(string id, bool? enabled, CancellationToken ct) =>
        ChangeFlag(enabled, value => users.SetPublishGrantAsync(ActorId, id, value, ct),
            enabled == true ? "Đã cấp quyền đăng bán." : "Đã thu hồi quyền đăng bán.");
    [HttpPost]
    public Task<IActionResult> Lock(string id, bool? locked, CancellationToken ct) =>
        ChangeFlag(locked, value => users.SetLockedAsync(ActorId, id, value, ct),
            locked == true ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.");
    [HttpPost]
    public Task<IActionResult> Trust(string id, int score, string reason, CancellationToken ct) =>
        Change(() => users.SetTrustScoreAsync(ActorId, id, score, reason, ct));
    private Task<IActionResult> ChangeFlag(bool? value, Func<bool, Task<AdminUserRow>> action, string successMessage)
    {
        if (!ModelState.IsValid || value is null)
        {
            TempData["Error"] = "Yêu cầu không hợp lệ. Hãy tải lại trang và thử lại.";
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Index)));
        }
        return Change(() => action(value.Value), successMessage);
    }
    private async Task<IActionResult> Change(Func<Task<AdminUserRow>> action, string successMessage = "Đã cập nhật tài khoản.")
    {
        try { await action(); TempData["Message"] = successMessage; }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
