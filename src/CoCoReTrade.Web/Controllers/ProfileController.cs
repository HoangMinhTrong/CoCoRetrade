using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class ProfileController(INotificationCenter notifications, ISocialService social,
    IAddressBookService addresses, IWalletService wallets, IUserProfileService profiles) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Balance = await wallets.BalanceAsync(UserId, ct);
        ViewBag.UserProfile = await profiles.GetAsync(UserId, ct);
        return View();
    }
    [HttpPost, RequestSizeLimit(6_000_000)]
    public async Task<IActionResult> Avatar(IFormFile image, CancellationToken ct)
    {
        if (image is null) { TempData["Error"] = "Chọn ảnh đại diện."; return RedirectToAction(nameof(Index)); }
        try
        {
            await profiles.SetAvatarAsync(UserId,
                new ImageUpload(image.FileName, image.ContentType, image.Length, image.OpenReadStream), ct);
            TempData["Message"] = "Đã cập nhật ảnh đại diện.";
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Notifications(string? group, CancellationToken ct)
    {
        ViewBag.Group = group;
        try { return View(await notifications.ListAsync(UserId, group, ct)); }
        catch (ArgumentException) { return BadRequest("Nhóm thông báo không hợp lệ."); }
    }
    [HttpPost]
    public async Task<IActionResult> ReadNotification(Guid id, CancellationToken ct)
    {
        try { await notifications.MarkReadAsync(UserId, id, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Notifications));
    }
    public async Task<IActionResult> Saved(CancellationToken ct) => View(await social.SavedProductsAsync(UserId, ct));
    public async Task<IActionResult> Addresses(CancellationToken ct) => View(await addresses.ListAsync(UserId, ct));
    [HttpGet]
    public async Task<IActionResult> Address(Guid? id, CancellationToken ct)
    {
        if (id is null) return View(new AddressForm());
        var existing = (await addresses.ListAsync(UserId, ct)).FirstOrDefault(x => x.Id == id);
        if (existing is null) return NotFound();
        return View(new AddressForm { Id = existing.Id, RecipientName = existing.RecipientName,
            RecipientPhone = existing.RecipientPhone, Province = existing.Province,
            Ward = existing.Ward, StreetAddress = existing.StreetAddress });
    }
    [HttpPost]
    public async Task<IActionResult> Address(AddressForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        try
        {
            await addresses.SaveAsync(UserId, form.ToInput(), ct);
            TempData["Message"] = "Đã lưu địa chỉ nhận hàng.";
            return RedirectToAction(nameof(Addresses));
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException)
        { ModelState.AddModelError(string.Empty, ex.Message); return View(form); }
    }
    [HttpPost]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct)
    {
        try { await addresses.DeleteAsync(UserId, id, ct); TempData["Message"] = "Đã xóa địa chỉ."; }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Addresses));
    }
    public async Task<IActionResult> Wallet(CancellationToken ct)
    {
        ViewBag.Withdrawals = await wallets.WithdrawalsAsync(UserId, ct);
        return View(await wallets.BalanceAsync(UserId, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Withdraw(WithdrawalForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Kiểm tra số tiền và tài khoản nhận."; return RedirectToAction(nameof(Wallet)); }
        try
        {
            await wallets.RequestWithdrawalAsync(UserId, form.RequestKey, form.Amount, form.BankAccountMasked, ct);
            TempData["Message"] = "Yêu cầu rút tiền đã được ghi nhận và đang chờ duyệt.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Wallet));
    }
}
