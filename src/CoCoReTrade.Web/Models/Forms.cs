using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Application;

namespace CoCoReTrade.Web.Models;

public sealed class RegisterForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(2, ErrorMessage = "Cần ít nhất {1} ký tự."), Display(Name = "Họ và tên")] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), EmailAddress(ErrorMessage = "Email không hợp lệ."), Display(Name = "Email")] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), RegularExpression(@"^0[35789]\d{8}$", ErrorMessage = "Nhập số di động Việt Nam gồm 10 chữ số."), Display(Name = "Số điện thoại")] public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(8, ErrorMessage = "Cần ít nhất {1} ký tự."), DataType(DataType.Password), Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp."), DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")] public string ConfirmPassword { get; set; } = "";
}
public sealed class LoginForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Email hoặc số điện thoại")] public string Identifier { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), DataType(DataType.Password), Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}
public sealed class ResetRequestForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Email hoặc số điện thoại")] public string Identifier { get; set; } = "";
}
public sealed class AccountOtpForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public string UserId { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Nhập mã gồm 6 chữ số."), Display(Name = "Mã xác thực")]
    public string Code { get; set; } = "";
}
public sealed class ResetPasswordForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public string UserId { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(8, ErrorMessage = "Cần ít nhất {1} ký tự."), DataType(DataType.Password), Display(Name = "Mật khẩu mới")] public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp."), DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")] public string ConfirmPassword { get; set; } = "";
}
public sealed class ChangePasswordForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này."), DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(8, ErrorMessage = "Cần ít nhất {1} ký tự."), DataType(DataType.Password), Display(Name = "Mật khẩu mới")] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp."), DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")] public string ConfirmPassword { get; set; } = "";
}
public sealed class ListingForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(3, ErrorMessage = "Cần ít nhất {1} ký tự."), Display(Name = "Tên sản phẩm")] public string Name { get; set; } = "";
    [Display(Name = "Thương hiệu (không bắt buộc)")] public string Brand { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Danh mục")] public string Category { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Kích cỡ")] public string Size { get; set; } = "";
    [Range(1000, 999999999999, ErrorMessage = "Số tiền phải từ 1,000 đến 999,999,999,999 VNĐ. Ví dụ: nhập 29000 cho 29,000 VNĐ."), Display(Name = "Giá gốc")] public decimal OriginalPrice { get; set; }
    [Range(1000, 999999999999, ErrorMessage = "Số tiền phải từ 1,000 đến 999,999,999,999 VNĐ. Ví dụ: nhập 29000 cho 29,000 VNĐ."), Display(Name = "Giá mong muốn")] public decimal AskingPrice { get; set; }
    [Range(50, 100, ErrorMessage = "Giá trị phải từ {1} đến {2}."), Display(Name = "Độ mới (%)")] public int ConditionPercent { get; set; } = 95;
    public bool AllowOffers { get; set; } = true;
    [Display(Name = "Ảnh sản phẩm")] public List<IFormFile> Images { get; set; } = [];
    public List<Guid> RetainedImageIds { get; set; } = [];
    public string ExpectedRowVersion { get; set; } = "";
    public SubmitListing ToRequest() => new(Name, Brand, Category, Size, OriginalPrice, AskingPrice, ConditionPercent,
        AllowOffers, Images.Select(f => new ImageUpload(f.FileName, f.ContentType, f.Length, f.OpenReadStream)).ToList());
    public EditListing ToEditRequest() => new(Name, Brand, Category, Size, OriginalPrice, AskingPrice,
        ConditionPercent, AllowOffers, RetainedImageIds,
        Images.Select(f => new ImageUpload(f.FileName, f.ContentType, f.Length, f.OpenReadStream)).ToList(), ExpectedRowVersion);
}
public sealed class InspectionForm
{
    public Guid ProductId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public string ExpectedRowVersion { get; set; } = "";
    public bool Passed { get; set; } = true;
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(3, ErrorMessage = "Cần ít nhất {1} ký tự."), Display(Name = "Tên sau kiểm định")] public string Name { get; set; } = "";
    [Display(Name = "Thương hiệu (không bắt buộc)")] public string Brand { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Danh mục")] public string Category { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Kích cỡ")] public string Size { get; set; } = "";
    [Range(1000, 999999999999, ErrorMessage = "Số tiền phải từ 1,000 đến 999,999,999,999 VNĐ. Ví dụ: nhập 29000 cho 29,000 VNĐ."), Display(Name = "Giá gốc")] public decimal OriginalPrice { get; set; }
    [Range(1000, 999999999999, ErrorMessage = "Số tiền phải từ 1,000 đến 999,999,999,999 VNĐ. Ví dụ: nhập 29000 cho 29,000 VNĐ."), Display(Name = "Giá niêm yết")] public decimal AskingPrice { get; set; }
    [Range(50, 100, ErrorMessage = "Giá trị phải từ {1} đến {2}."), Display(Name = "Độ mới thực tế (%)")] public int VerifiedCondition { get; set; }
    [StringLength(2000, ErrorMessage = "Ghi chú kiểm định không được vượt quá 2,000 ký tự."), Display(Name = "Ghi chú kiểm định")]
    public string? Notes { get; set; }
    [Display(Name = "Lý do từ chối")] public string RejectionReason { get; set; } = "";
    [Display(Name = "Ảnh kiểm định bổ sung")] public List<IFormFile> InspectionImages { get; set; } = [];
    public InspectionSubmission ToRequest() => new(Passed, Name, Brand, Category, Size, OriginalPrice,
        AskingPrice, VerifiedCondition, Notes, ExpectedRowVersion,
        InspectionImages.Select(f => new ImageUpload(f.FileName, f.ContentType, f.Length, f.OpenReadStream)).ToList());
}

public sealed class ProductDecisionForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public string ExpectedRowVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool PhysicalItemConfirmed { get; set; }
}
