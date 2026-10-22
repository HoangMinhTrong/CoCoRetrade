using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record MobileLoginRequest(string Identifier, string Password);
public sealed record MobileRegisterRequest(string FullName, string Email, string Phone, string Password);
public sealed record MobileOtpRequest(string UserId, string Code);
public sealed record MobileIdentifierRequest(string Identifier);
public sealed record MobileResetRequest(string UserId, string Token, string Password);
public sealed record MobileChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record MobileAccountDto(string Id, string FullName, string Email, string? Phone,
    bool MustChangePassword, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

// The mobile client uses the existing Identity cookie and the same antiforgery protection as MVC.
[ApiController]
[Route("api/v1/mobile/account")]
public sealed class MobileAccountController(IAccountService accounts, IAccountOtpService otps,
    IAccountMessageSender messages, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn) : ControllerBase
{
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<MobileAccountDto>> Me() =>
        Ok(await CurrentAsync());

    [HttpPost("login")]
    public async Task<ActionResult<MobileAccountDto>> Login(MobileLoginRequest request)
    {
        var result = await accounts.LoginAsync(request.Identifier, request.Password, true);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        var user = await users.FindByIdAsync(result.UserId!);
        return Ok(await ToDtoAsync(user!));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(MobileRegisterRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(new RegisterAccount(
            request.FullName, request.Email, request.Phone, request.Password));
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        try
        {
            var otp = await otps.IssueActivationAsync(result.UserId!, ct);
            await messages.SendOtpAsync(otp.Email, otp.Code, "activation");
            return Ok(new { userId = result.UserId, message = "Đã gửi mã kích hoạt đến email." });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
        {
            return StatusCode(503, new { userId = result.UserId,
                error = "Tài khoản đã tạo nhưng chưa gửi được mã kích hoạt. Hãy gửi lại mã sau." });
        }
    }

    [HttpPost("resend-activation")]
    public async Task<IActionResult> ResendActivation(MobileIdentifierRequest request, CancellationToken ct)
    {
        var account = await accounts.FindUnconfirmedAsync(request.Identifier);
        if (account.UserId is not null)
        {
            try
            {
                var otp = await otps.IssueActivationAsync(account.UserId, ct);
                await messages.SendOtpAsync(otp.Email, otp.Code, "activation");
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
            { return StatusCode(503, new { error = "Chưa gửi được mã kích hoạt. Vui lòng thử lại." }); }
        }
        return Ok(new { userId = account.UserId,
            message = "Nếu tài khoản đang chờ kích hoạt, mã mới đã được gửi." });
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate(MobileOtpRequest request, CancellationToken ct)
    {
        var result = await otps.ConfirmActivationAsync(request.UserId, request.Code, ct);
        return result.Succeeded ? Ok(new { message = "Tài khoản đã được kích hoạt." })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(MobileIdentifierRequest request, CancellationToken ct)
    {
        var account = await accounts.CreateResetTokenAsync(request.Identifier);
        if (account.UserId is not null)
        {
            try
            {
                var otp = await otps.IssueResetAsync(account.UserId, ct);
                await messages.SendOtpAsync(otp.Email, otp.Code, "reset");
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
            { return StatusCode(503, new { error = "Chưa gửi được mã đặt lại mật khẩu." }); }
        }
        return Ok(new { userId = account.UserId,
            message = "Nếu tài khoản hợp lệ, mã đặt lại mật khẩu đã được gửi." });
    }

    [HttpPost("verify-reset")]
    public async Task<IActionResult> VerifyReset(MobileOtpRequest request, CancellationToken ct)
    {
        var result = await otps.VerifyResetAsync(request.UserId, request.Code, ct);
        return result.Succeeded ? Ok(new { userId = result.UserId, token = result.Token })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(MobileResetRequest request)
    {
        var result = await accounts.ResetPasswordAsync(request.UserId, request.Token, request.Password);
        return result.Succeeded ? Ok(new { message = "Mật khẩu đã được đặt lại." })
            : BadRequest(new { error = result.Error });
    }

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(MobileChangePasswordRequest request)
    {
        var result = await accounts.ChangePasswordAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            request.CurrentPassword, request.NewPassword);
        return result.Succeeded ? Ok(new { message = "Mật khẩu đã được thay đổi." })
            : BadRequest(new { error = result.Error });
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await accounts.LogoutAsync();
        return NoContent();
    }

    private async Task<MobileAccountDto> CurrentAsync()
    {
        var user = await users.GetUserAsync(User)
            ?? throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn.");
        return await ToDtoAsync(user);
    }

    private async Task<MobileAccountDto> ToDtoAsync(ApplicationUser user)
    {
        var principal = await signIn.CreateUserPrincipalAsync(user);
        return new MobileAccountDto(user.Id, user.FullName, user.Email ?? "", user.PhoneNumber,
            user.MustChangePassword, (await users.GetRolesAsync(user)).ToArray(),
            principal.FindAll(StaffPermissions.ClaimType)
                .Select(x => x.Value).ToArray());
    }
}
