using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class OffersController(IOfferService offers, IProductService products, MarketplacePolicy policy) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await offers.MineAsync(UserId, ct));

    [HttpGet]
    public async Task<IActionResult> Make(Guid productId, CancellationToken ct)
    {
        var product = await products.VisibleDetailsAsync(productId, UserId, User.IsInRole("Admin"), ct);
        if (product is null || product.Status != ProductStatus.Active || !product.AllowOffers || product.SellerId == UserId) return NotFound();
        ViewBag.Product = product;
        ViewBag.MinimumPrice = policy.MinimumOffer(product.AskingPrice);
        return View(new MakeOfferForm { ProductId = productId });
    }
    [HttpPost]
    public async Task<IActionResult> Make(MakeOfferForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await RenderMake(form, ct);
        try
        {
            var offer = await offers.MakeAsync(form.ProductId, UserId, form.Price, ct);
            TempData["Message"] = "Đã gửi giá đề xuất cho người bán.";
            return RedirectToAction(nameof(Details), new { id = offer.Id });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException or DbUpdateException)
        { ModelState.AddModelError(string.Empty, ex.Message); return await RenderMake(form, ct); }
    }
    private async Task<IActionResult> RenderMake(MakeOfferForm form, CancellationToken ct)
    {
        var product = await products.VisibleDetailsAsync(form.ProductId, UserId, User.IsInRole("Admin"), ct);
        if (product is null) return NotFound();
        ViewBag.Product = product; ViewBag.MinimumPrice = policy.MinimumOffer(product.AskingPrice);
        return View("Make", form);
    }
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var offer = await offers.DetailsAsync(id, UserId, ct);
        return offer is null ? NotFound() : View(offer);
    }
    [HttpPost]
    public async Task<IActionResult> Respond(Guid id, OfferResponse? action, decimal? price, CancellationToken ct)
    {
        if (!ModelState.IsValid || action is null || !Enum.IsDefined(action.Value))
        { TempData["Error"] = "Yêu cầu phản hồi không hợp lệ."; return RedirectToAction(nameof(Details), new { id }); }
        try
        {
            await offers.RespondAsync(id, UserId, action.Value, price, ct);
            TempData["Message"] = "Đã cập nhật đề xuất giá.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException or DbUpdateException)
        { TempData["Error"] = ex.Message.Split('\n')[0].TrimEnd('\r'); }
        return RedirectToAction(nameof(Details), new { id });
    }
}
