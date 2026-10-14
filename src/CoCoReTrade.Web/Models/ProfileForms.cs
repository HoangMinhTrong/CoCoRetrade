using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Application;

namespace CoCoReTrade.Web.Models;

public sealed class AddressForm
{
    public Guid? Id { get; set; }
    [Required, MinLength(2), MaxLength(150), Display(Name = "Người nhận")]
    public string RecipientName { get; set; } = "";
    [Required, RegularExpression(@"^0[35789][0-9]{8}$"), Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = "";
    [Required, MinLength(2), MaxLength(120), Display(Name = "Tỉnh / thành phố")]
    public string Province { get; set; } = "";
    [Required, MinLength(2), MaxLength(120), Display(Name = "Phường / xã")]
    public string Ward { get; set; } = "";
    [Required, MinLength(4), MaxLength(300), Display(Name = "Số nhà, đường")]
    public string StreetAddress { get; set; } = "";
    public AddressInput ToInput() => new(Id, RecipientName, RecipientPhone, Province, Ward, StreetAddress);
}

public sealed class WithdrawalForm
{
    public Guid RequestKey { get; set; } = Guid.NewGuid();
    [Range(1, 999999999999999999d), Display(Name = "Số tiền rút (VNĐ)")]
    public decimal Amount { get; set; }
    [Required, MaxLength(60), Display(Name = "Tài khoản nhận đã che số")]
    public string BankAccountMasked { get; set; } = "";
}
