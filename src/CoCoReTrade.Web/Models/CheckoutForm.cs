using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Web.Models;

public sealed class CheckoutForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public Guid ProductId { get; set; }
    public Guid? OfferId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public Guid IdempotencyKey { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage = "Vui lòng nhập trường này."), MinLength(2, ErrorMessage = "Cần ít nhất {1} ký tự."), MaxLength(150, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Tên người nhận")]
    public string RecipientName { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), RegularExpression(@"^0[35789][0-9]{8}$", ErrorMessage = "Nhập số di động Việt Nam gồm 10 chữ số."), Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập trường này."), Display(Name = "Hình thức nhận")]
    public FulfillmentType Fulfillment { get; set; } = FulfillmentType.Pickup;
    [Display(Name = "Ngày giờ tới lấy")]
    public string? PickupLocal { get; set; }
    [MaxLength(120, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Tỉnh / thành phố")]
    public string? Province { get; set; }
    [MaxLength(120, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Phường / xã")]
    public string? Ward { get; set; }
    [MaxLength(300, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Số nhà, đường và địa chỉ cụ thể")]
    public string? StreetAddress { get; set; }
    [MaxLength(500, ErrorMessage = "Tối đa {1} ký tự."), Display(Name = "Ghi chú cho người giao")]
    public string? DeliveryNote { get; set; }
    public CheckoutRequest ToRequest() => new(ProductId, OfferId, IdempotencyKey,
        RecipientName, RecipientPhone, Fulfillment, PickupLocal, Province, Ward, StreetAddress,
        null, DeliveryNote);
}
