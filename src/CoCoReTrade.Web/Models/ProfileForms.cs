using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Application;

namespace CoCoReTrade.Web.Models;

public sealed class AddressForm
{
    public Guid? Id { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(2, ErrorMessage = "Cần ít nhất {1} ký tự."), MaxLength(150, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Người nhận")]
    public string RecipientName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), RegularExpression(@"^0[35789][0-9]{8}$", ErrorMessage = "Nhập số di động Việt Nam gồm 10 chữ số."), Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(2, ErrorMessage = "Cần ít nhất {1} ký tự."), MaxLength(120, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Tỉnh / thành phố")]
    public string Province { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(2, ErrorMessage = "Cần ít nhất {1} ký tự."), MaxLength(120, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Phường / xã")]
    public string Ward { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(4, ErrorMessage = "Cần ít nhất {1} ký tự."), MaxLength(300, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Số nhà, đường")]
    public string StreetAddress { get; set; } = "";
    public AddressInput ToInput() => new(Id, RecipientName, RecipientPhone, Province, Ward, StreetAddress);
}

public sealed class WithdrawalForm
{
    public Guid RequestKey { get; set; } = Guid.NewGuid();
    [Range(1, 999999999999999999d, ErrorMessage = "Số tiền rút phải lớn hơn 0."), Display(Name = "Số tiền rút (VNĐ)")]
    public decimal Amount { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập trường này."), RegularExpression(@"^[0-9]{6,30}$", ErrorMessage = "Số tài khoản gồm 6–30 chữ số."),
     Display(Name = "Số tài khoản ngân hàng")]
    public string BankAccountNumber { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(2, ErrorMessage = "Cần ít nhất {1} ký tự."), MaxLength(100, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Tên ngân hàng")]
    public string BankName { get; set; } = "";
}
