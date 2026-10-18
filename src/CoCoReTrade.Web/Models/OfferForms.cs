using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Web.Models;

public sealed class MakeOfferForm
{
    [Required(ErrorMessage = "Vui lòng nhập trường này.")] public Guid ProductId { get; set; }
    [Range(1, 999999999999, ErrorMessage = "Số tiền phải từ 1 đến 999,999,999,999 VNĐ."), Display(Name = "Giá đề xuất (VNĐ)")] public decimal Price { get; set; }
}
