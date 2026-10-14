using System.Text.RegularExpressions;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AccountService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : IAccountService
{
    public async Task<AccountActionResult> RegisterAsync(RegisterAccount request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (request.FullName.Trim().Length < 2 || !Regex.IsMatch(request.Phone, @"^0[35789]\d{8}$"))
            return new(false, "Họ tên hoặc số điện thoại không hợp lệ.");
        if (await users.FindByEmailAsync(email) is not null || await users.Users.AnyAsync(x => x.PhoneNumber == request.Phone))
            return new(false, "Email hoặc số điện thoại đã được sử dụng.");
        var user = new ApplicationUser { UserName = email, Email = email, PhoneNumber = request.Phone,
            FullName = request.FullName.Trim(), EmailConfirmed = false };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return new(false, string.Join(" ", result.Errors.Select(x => x.Description)));
        var roleResult = await users.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            return new(false, "Không thể hoàn tất tạo tài khoản. Vui lòng thử lại.");
        }
        return new(true, UserId: user.Id, Email: email);
    }
    public async Task<AccountActionResult> ConfirmEmailAsync(string userId, string token)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return new(false, "Liên kết không hợp lệ.");
        var result = await users.ConfirmEmailAsync(user, token);
        return result.Succeeded ? new(true) : new(false, "Liên kết đã hết hạn hoặc không hợp lệ.");
    }
    public async Task<AccountActionResult> LoginAsync(string identifier, string password, bool rememberMe)
    {
        var key = identifier.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(key) ?? await users.Users.FirstOrDefaultAsync(x => x.PhoneNumber == key);
        if (user is null) return new(false, "Thông tin đăng nhập không đúng.");
        if (!user.EmailConfirmed) return new(false, "Tài khoản chưa được kích hoạt. Hãy dùng mã xác thực đã gửi đến email.");
        if (user.TrustSuspended) return new(false, "Tài khoản tạm khóa do điểm tín nhiệm thấp. Vui lòng liên hệ CoCo.");
        var result = await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut) return new(false, "Tài khoản tạm khóa do đăng nhập sai nhiều lần.");
        if (!result.Succeeded) return new(false, "Thông tin đăng nhập không đúng.");
        return new(true, UserId: user.Id);
    }
    public Task LogoutAsync() => signIn.SignOutAsync();
    public async Task<AccountActionResult> CreateResetTokenAsync(string identifier)
    {
        var key = identifier.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(key) ?? await users.Users.FirstOrDefaultAsync(x => x.PhoneNumber == key);
        if (user is null || !user.EmailConfirmed) return new(true);
        return new(true, UserId: user.Id, Email: user.Email);
    }
    public async Task<AccountActionResult> FindUnconfirmedAsync(string identifier)
    {
        var key = identifier.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(key) ?? await users.Users.FirstOrDefaultAsync(x => x.PhoneNumber == key);
        return user is { EmailConfirmed: false } ? new(true, UserId: user.Id, Email: user.Email) : new(true);
    }
    public async Task<AccountActionResult> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return new(false, "Yêu cầu không hợp lệ.");
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded) return new(false, string.Join(" ", result.Errors.Select(x => x.Description)));
        await users.UpdateSecurityStampAsync(user);
        await signIn.SignInAsync(user, isPersistent: false);
        return new(true);
    }
    public async Task<AccountActionResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return new(false, "Tài khoản không tồn tại.");
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) return new(false, string.Join(" ", result.Errors.Select(x => x.Description)));
        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);
        return new(true);
    }
}
