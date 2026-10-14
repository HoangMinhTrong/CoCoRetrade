using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class ListingController(IProductService products) : Controller
{
    [HttpGet] public IActionResult Create() => View(new ListingForm());
    [HttpPost]
    [RequestSizeLimit(55_000_000)]
    public async Task<IActionResult> Create(ListingForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        try
        {
            var product = await products.SubmitAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, form.ToRequest(), ct);
            TempData["Message"] = $"Đã gửi ký gửi {product.ConsignmentCode}. Vui lòng mang đồ đến CoCo để kiểm định.";
            return RedirectToAction(nameof(Closet));
        }
        catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(form); }
    }
    [HttpGet]
    public async Task<IActionResult> Closet(CancellationToken ct) =>
        View(await products.ClosetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
}
