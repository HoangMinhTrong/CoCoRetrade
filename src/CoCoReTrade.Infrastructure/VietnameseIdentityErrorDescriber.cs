using Microsoft.AspNetCore.Identity;

namespace CoCoReTrade.Infrastructure;

public sealed class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(base.DefaultError(), "Không thể hoàn tất thao tác. Vui lòng thử lại.");
    public override IdentityError ConcurrencyFailure() => Error(base.ConcurrencyFailure(), "Dữ liệu đã thay đổi. Vui lòng tải lại trang.");
    public override IdentityError PasswordMismatch() => Error(base.PasswordMismatch(), "Mật khẩu hiện tại không đúng.");
    public override IdentityError InvalidToken() => Error(base.InvalidToken(), "Mã xác thực không hợp lệ hoặc đã hết hạn.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(base.RecoveryCodeRedemptionFailed(), "Mã khôi phục không hợp lệ.");
    public override IdentityError LoginAlreadyAssociated() => Error(base.LoginAlreadyAssociated(), "Cách đăng nhập này đã liên kết với tài khoản khác.");
    public override IdentityError InvalidUserName(string? userName) => Error(base.InvalidUserName(userName), "Tên đăng nhập không hợp lệ.");
    public override IdentityError InvalidEmail(string? email) => Error(base.InvalidEmail(email), "Email không hợp lệ.");
    public override IdentityError DuplicateUserName(string userName) => Error(base.DuplicateUserName(userName), "Tên đăng nhập đã được sử dụng.");
    public override IdentityError DuplicateEmail(string email) => Error(base.DuplicateEmail(email), "Email đã được sử dụng.");
    public override IdentityError InvalidRoleName(string? role) => Error(base.InvalidRoleName(role), "Tên vai trò không hợp lệ.");
    public override IdentityError DuplicateRoleName(string role) => Error(base.DuplicateRoleName(role), "Vai trò này đã tồn tại.");
    public override IdentityError UserAlreadyHasPassword() => Error(base.UserAlreadyHasPassword(), "Tài khoản đã có mật khẩu.");
    public override IdentityError UserLockoutNotEnabled() => Error(base.UserLockoutNotEnabled(), "Tài khoản chưa bật chức năng khóa tạm thời.");
    public override IdentityError UserAlreadyInRole(string role) => Error(base.UserAlreadyInRole(role), "Tài khoản đã có vai trò này.");
    public override IdentityError UserNotInRole(string role) => Error(base.UserNotInRole(role), "Tài khoản chưa có vai trò này.");
    public override IdentityError PasswordTooShort(int length) => Error(base.PasswordTooShort(length), $"Mật khẩu phải có ít nhất {length} ký tự.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(base.PasswordRequiresNonAlphanumeric(), "Mật khẩu cần có ký tự đặc biệt.");
    public override IdentityError PasswordRequiresDigit() => Error(base.PasswordRequiresDigit(), "Mật khẩu cần có ít nhất một chữ số.");
    public override IdentityError PasswordRequiresLower() => Error(base.PasswordRequiresLower(), "Mật khẩu cần có ít nhất một chữ thường.");
    public override IdentityError PasswordRequiresUpper() => Error(base.PasswordRequiresUpper(), "Mật khẩu cần có ít nhất một chữ hoa.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(base.PasswordRequiresUniqueChars(uniqueChars), $"Mật khẩu cần có ít nhất {uniqueChars} ký tự khác nhau.");

    private static IdentityError Error(IdentityError original, string description) => new()
    {
        Code = original.Code,
        Description = description
    };
}
