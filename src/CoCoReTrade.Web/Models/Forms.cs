using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Application;

namespace CoCoReTrade.Web.Models;

public sealed class RegisterForm
{
    [Required, MinLength(2), Display(Name = "Họ và tên")] public string FullName { get; set; } = "";
    [Required, EmailAddress, Display(Name = "Email")] public string Email { get; set; } = "";
    [Required, RegularExpression(@"^0[35789]\d{8}$", ErrorMessage = "Nhập số di động Việt Nam gồm 10 chữ số."), Display(Name = "Số điện thoại")] public string Phone { get; set; } = "";
    [Required, MinLength(8), DataType(DataType.Password), Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    [Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")] public string ConfirmPassword { get; set; } = "";
}
public sealed class LoginForm
{
    [Required, Display(Name = "Email hoặc số điện thoại")] public string Identifier { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
}
public sealed class ResetRequestForm
{
    [Required, Display(Name = "Email hoặc số điện thoại")] public string Identifier { get; set; } = "";
}
public sealed class AccountOtpForm
{
    [Required] public string UserId { get; set; } = "";
    [Required, RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Nhập mã gồm 6 chữ số."), Display(Name = "Mã xác thực")]
    public string Code { get; set; } = "";
}
public sealed class ResetPasswordForm
{
    [Required] public string UserId { get; set; } = "";
    [Required] public string Token { get; set; } = "";
    [Required, MinLength(8), DataType(DataType.Password), Display(Name = "Mật khẩu mới")] public string Password { get; set; } = "";
    [Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")] public string ConfirmPassword { get; set; } = "";
}
public sealed class ChangePasswordForm
{
    [Required, DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(8), DataType(DataType.Password), Display(Name = "Mật khẩu mới")] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword)), DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")] public string ConfirmPassword { get; set; } = "";
}
public sealed class ListingForm
{
    [Required, MinLength(3), Display(Name = "Tên sản phẩm")] public string Name { get; set; } = "";
    [Required, MinLength(2), Display(Name = "Thương hiệu")] public string Brand { get; set; } = "";
    [Required, Display(Name = "Danh mục")] public string Category { get; set; } = "";
    [Required, Display(Name = "Kích cỡ")] public string Size { get; set; } = "";
    [Range(1, 999999999999), Display(Name = "Giá gốc")] public decimal OriginalPrice { get; set; }
    [Range(1, 999999999999), Display(Name = "Giá mong muốn")] public decimal AskingPrice { get; set; }
    [Range(50, 100), Display(Name = "Độ mới (%)")] public int ConditionPercent { get; set; } = 95;
    public bool AllowOffers { get; set; } = true;
    [Display(Name = "Ảnh sản phẩm")] public List<IFormFile> Images { get; set; } = [];
    public SubmitListing ToRequest() => new(Name, Brand, Category, Size, OriginalPrice, AskingPrice, ConditionPercent,
        AllowOffers, Images.Select(f => new ImageUpload(f.FileName, f.ContentType, f.Length, f.OpenReadStream)).ToList());
}
public sealed class InspectionForm
{
    public Guid ProductId { get; set; }
    [Required] public string ExpectedRowVersion { get; set; } = "";
    public bool Passed { get; set; } = true;
    [Required, MinLength(3), Display(Name = "Tên sau kiểm định")] public string Name { get; set; } = "";
    [Required, MinLength(2), Display(Name = "Thương hiệu")] public string Brand { get; set; } = "";
    [Required, Display(Name = "Danh mục")] public string Category { get; set; } = "";
    [Required, Display(Name = "Kích cỡ")] public string Size { get; set; } = "";
    [Range(1, 999999999999), Display(Name = "Giá gốc")] public decimal OriginalPrice { get; set; }
    [Range(1, 999999999999), Display(Name = "Giá niêm yết")] public decimal AskingPrice { get; set; }
    [Range(50, 100), Display(Name = "Độ mới thực tế (%)")] public int VerifiedCondition { get; set; }
    [Display(Name = "Ghi chú kiểm định")] public string Notes { get; set; } = "";
    [Display(Name = "Lý do từ chối")] public string RejectionReason { get; set; } = "";
    [Display(Name = "Ảnh kiểm định bổ sung")] public List<IFormFile> InspectionImages { get; set; } = [];
    public InspectionSubmission ToRequest() => new(Passed, Name, Brand, Category, Size, OriginalPrice,
        AskingPrice, VerifiedCondition, Notes, ExpectedRowVersion,
        InspectionImages.Select(f => new ImageUpload(f.FileName, f.ContentType, f.Length, f.OpenReadStream)).ToList());
}

public sealed class ProductDecisionForm
{
    [Required] public string ExpectedRowVersion { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ConfirmedSku { get; set; } = "";
    public bool PhysicalItemConfirmed { get; set; }
}
