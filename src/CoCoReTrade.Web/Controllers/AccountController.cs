using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class AccountController(IAccountService accounts, IAccountMessageSender messages,
    IAccountOtpService otps) : Controller
{
    [HttpGet] public IActionResult Register() => View(new RegisterForm());
    [HttpPost]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await accounts.RegisterAsync(new RegisterAccount(form.FullName, form.Email, form.Phone, form.Password));
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }
        IssuedAccountOtp issued;
        try
        {
            issued = await otps.IssueActivationAsync(result.UserId!, HttpContext.RequestAborted);
            await messages.SendOtpAsync(issued.Email, issued.Code, "activation");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
        {
            TempData["Error"] = "Tài khoản đã tạo nhưng chưa gửi được mã. Hãy dùng chức năng gửi lại mã kích hoạt.";
            return RedirectToAction(nameof(ResendActivation));
        }
        ViewBag.UserId = result.UserId;
        ViewBag.Flow = "activation";
        return View("CheckEmail");
    }
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        var result = await accounts.ConfirmEmailAsync(userId, token);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? "Tài khoản đã kích hoạt. Bạn có thể đăng nhập." : result.Error;
        return RedirectToAction(nameof(Login));
    }
    [HttpGet]
    public IActionResult ConfirmOtp(string userId) => View(new AccountOtpForm { UserId = userId });
    [HttpPost]
    public async Task<IActionResult> ConfirmOtp(AccountOtpForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await otps.ConfirmActivationAsync(form.UserId, form.Code, ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }
        TempData["Message"] = "Tài khoản đã kích hoạt. Bạn có thể đăng nhập.";
        return RedirectToAction(nameof(Login));
    }
    [HttpGet] public IActionResult Login() => View(new LoginForm());
    [HttpGet] public IActionResult ResendActivation() => View(new ResetRequestForm());
    [HttpPost]
    public async Task<IActionResult> ResendActivation(ResetRequestForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var account = await accounts.FindUnconfirmedAsync(form.Identifier);
        if (account.UserId is not null)
        {
            try
            {
                var issued = await otps.IssueActivationAsync(account.UserId, ct);
                await messages.SendOtpAsync(issued.Email, issued.Code, "activation");
                ViewBag.UserId = account.UserId;
                ViewBag.Flow = "activation";
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
            {
                ModelState.AddModelError(string.Empty, ex is System.Net.Mail.SmtpException ?
                    "Không gửi được email lúc này. Vui lòng thử lại sau." : ex.Message);
                return View(form);
            }
        }
        else
        {
            ViewBag.UserId = Guid.NewGuid().ToString("N");
            ViewBag.Flow = "activation";
        }
        return View("CheckEmail");
    }
    [HttpPost]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await accounts.LoginAsync(form.Identifier, form.Password, form.RememberMe);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }
        return RedirectToAction("Index", "Home");
    }
    [Authorize, HttpPost] public async Task<IActionResult> Logout()
    { await accounts.LogoutAsync(); return RedirectToAction("Index", "Home"); }

    [HttpGet] public IActionResult ForgotPassword() => View(new ResetRequestForm());
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ResetRequestForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await accounts.CreateResetTokenAsync(form.Identifier);
        if (result.UserId is not null)
        {
            IssuedAccountOtp issued;
            try
            {
                issued = await otps.IssueResetAsync(result.UserId, HttpContext.RequestAborted);
                await messages.SendOtpAsync(issued.Email, issued.Code, "reset");
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
            {
                ModelState.AddModelError(string.Empty, ex is System.Net.Mail.SmtpException ?
                    "Không gửi được email lúc này. Vui lòng thử lại sau." : ex.Message);
                return View(form);
            }
            ViewBag.UserId = result.UserId;
            ViewBag.Flow = "reset";
        }
        else
        {
            ViewBag.UserId = Guid.NewGuid().ToString("N");
            ViewBag.Flow = "reset";
        }
        return View("CheckEmail");
    }
    [HttpGet]
    public IActionResult VerifyResetOtp(string userId) => View(new AccountOtpForm { UserId = userId });
    [HttpPost]
    public async Task<IActionResult> VerifyResetOtp(AccountOtpForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await otps.VerifyResetAsync(form.UserId, form.Code, ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }
        return View("ResetPassword", new ResetPasswordForm { UserId = result.UserId!, Token = result.Token! });
    }
    [HttpPost]
    public async Task<IActionResult> ResendOtp(string userId, string purpose, CancellationToken ct)
    {
        if (purpose is not ("activation" or "reset")) return BadRequest();
        try
        {
            var issued = purpose == "activation" ? await otps.IssueActivationAsync(userId, ct)
                : await otps.IssueResetAsync(userId, ct);
            await messages.SendOtpAsync(issued.Email, issued.Code, purpose);
            TempData["Message"] = "Đã gửi mã xác thực mới.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or System.Net.Mail.SmtpException)
        {
            if (ex is KeyNotFoundException) TempData["Message"] = "Nếu tài khoản hợp lệ, mã mới đã được gửi.";
            else TempData["Error"] = ex is System.Net.Mail.SmtpException ?
                "Không gửi được email lúc này. Vui lòng thử lại sau." : ex.Message;
        }
        return RedirectToAction(purpose == "activation" ? nameof(ConfirmOtp) : nameof(VerifyResetOtp), new { userId });
    }
    [HttpGet] public IActionResult ResetPassword(string userId, string token) => View(new ResetPasswordForm { UserId = userId, Token = token });
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await accounts.ResetPasswordAsync(form.UserId, form.Token, form.Password);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }
        TempData["Message"] = "Mật khẩu đã được cập nhật và bạn đã đăng nhập vào phiên mới.";
        return RedirectToAction("Index", "Home");
    }
    [Authorize, HttpGet] public IActionResult ChangePassword() => View(new ChangePasswordForm());
    [Authorize, HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await accounts.ChangePasswordAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, form.CurrentPassword, form.NewPassword);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }
        TempData["Message"] = "Mật khẩu đã đổi thành công.";
        return RedirectToAction("Index", "Home");
    }
    public IActionResult AccessDenied() => View();
}
