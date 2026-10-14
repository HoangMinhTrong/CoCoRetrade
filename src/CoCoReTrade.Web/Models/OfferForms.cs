using System.ComponentModel.DataAnnotations;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Web.Models;

public sealed class MakeOfferForm
{
    [Required] public Guid ProductId { get; set; }
    [Range(1, 999999999999), Display(Name = "Giá đề xuất (VNĐ)")] public decimal Price { get; set; }
}
