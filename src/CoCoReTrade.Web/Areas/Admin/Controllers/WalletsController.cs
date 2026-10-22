using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = StaffPermissions.ManageWallets)]
public sealed class WalletsController(IAdminWalletService wallets, IWalletService ledger,
    IPlatformCommissionService platformCommissions) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Withdrawals = await wallets.WithdrawalsAsync(ActorId, ct);
        ViewBag.Candidates = await wallets.PayoutCandidatesAsync(ActorId, ct);
        return View(await wallets.ListAsync(ActorId, ct));
    }
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var wallet = await wallets.DetailsAsync(ActorId, id, ct);
        return wallet is null ? NotFound() : View(wallet);
    }
    [Authorize(Roles = "Admin"), HttpGet]
    public async Task<IActionResult> Platform(CancellationToken ct) =>
        View(await platformCommissions.GetAsync(ActorId, ct));
    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<IActionResult> WithdrawPlatform(Guid requestKey, decimal amount,
        string? bankAccountNumber, string? bankName, CancellationToken ct)
    {
        try
        {
            await platformCommissions.WithdrawAsync(ActorId, requestKey, amount, bankAccountNumber, bankName, ct);
            TempData["Message"] = "Đã ghi nhận rút hoa hồng và trừ số dư ví CoCo. Bạn tự thực hiện chuyển khoản ngoài hệ thống.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Platform));
    }
    [HttpPost]
    public async Task<IActionResult> Credit(Guid orderId, bool confirmed, CancellationToken ct)
    {
        try
        {
            var wallet = await wallets.CreditCompletedOrderAsync(ActorId, orderId, confirmed, ct);
            TempData["Message"] = "Đã ghi có tiền bán hàng vào ví và lưu nhật ký đối soát.";
            return RedirectToAction(nameof(Details), new { id = wallet.Wallet.Id });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }
    [HttpPost]
    public async Task<IActionResult> DecideWithdrawal(Guid id, bool approve, string? reason, CancellationToken ct)
    {
        try
        {
            await ledger.DecideWithdrawalAsync(ActorId, id, approve, reason, ct);
            TempData["Message"] = approve ? "Đã duyệt yêu cầu rút; chưa xác nhận chuyển khoản ngân hàng." :
                "Đã từ chối yêu cầu rút và hoàn lại số dư ví.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> ConfirmTransfer(Guid id, string transferReference, bool confirmed, CancellationToken ct)
    {
        try
        {
            if (!confirmed) throw new ArgumentException("Xác nhận đã chuyển khoản thực tế trước khi lưu.");
            await ledger.ConfirmTransferAsync(ActorId, id, transferReference, ct);
            TempData["Message"] = "Đã ghi nhận giao dịch chuyển tiền. Người bán có thể xem trạng thái và mã giao dịch trong ví.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
