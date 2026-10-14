using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Web.Models;

public sealed class CheckoutForm
{
    [Required] public Guid ProductId { get; set; }
    public Guid? OfferId { get; set; }
    [Required] public Guid IdempotencyKey { get; set; } = Guid.NewGuid();
    [Required, MinLength(2), MaxLength(150), Display(Name = "Tên người nhận")]
    public string RecipientName { get; set; } = "";
    [Required, RegularExpression(@"^0[35789][0-9]{8}$", ErrorMessage = "Nhập số di động Việt Nam gồm 10 chữ số."), Display(Name = "Số điện thoại")]
    public string RecipientPhone { get; set; } = "";
    [Required, Display(Name = "Hình thức nhận")]
    public FulfillmentType Fulfillment { get; set; } = FulfillmentType.Pickup;
    [Display(Name = "Ngày giờ tới lấy")]
    public string? PickupLocal { get; set; }
    [MaxLength(120), Display(Name = "Tỉnh / thành phố")]
    public string? Province { get; set; }
    [MaxLength(120), Display(Name = "Phường / xã")]
    public string? Ward { get; set; }
    [MaxLength(300), Display(Name = "Số nhà, đường và địa chỉ cụ thể")]
    public string? StreetAddress { get; set; }
    [Display(Name = "Khung giờ giao thuận tiện")]
    public DeliveryWindow? PreferredDeliveryWindow { get; set; }
    [MaxLength(500), Display(Name = "Ghi chú cho người giao")]
    public string? DeliveryNote { get; set; }
    public CheckoutRequest ToRequest() => new(ProductId, OfferId, IdempotencyKey,
        RecipientName, RecipientPhone, Fulfillment, PickupLocal, Province, Ward, StreetAddress,
        PreferredDeliveryWindow, DeliveryNote);
}
