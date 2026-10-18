using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

[Authorize]
public sealed class CheckoutController(IOrderService orders, IProductService products, ShippingPolicy shipping,
    IAddressBookService addresses, IConfiguration configuration) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(Guid productId, Guid? offerId, FulfillmentType? fulfillment, CancellationToken ct)
    {
        try
        {
            var form = new CheckoutForm { ProductId = productId, OfferId = offerId,
                Fulfillment = fulfillment == FulfillmentType.Shipping || !shipping.CanPickup
                    ? FulfillmentType.Shipping : FulfillmentType.Pickup };
            await PrepareView(form, ct);
            return View(form);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or UnauthorizedAccessException or InvalidOperationException)
        { TempData["Error"] = ex.Message; return RedirectToAction("Index", "Shop"); }
    }
    [HttpPost]
    public async Task<IActionResult> Index(CheckoutForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Render(form, ct);
        try
        {
            var order = await orders.PlaceAsync(form.ToRequest(), UserId, ct);
            TempData["Message"] = "Đơn hàng đã được ghi nhận và đang chờ xử lý.";
            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
        { ModelState.AddModelError(string.Empty, ex.Message); return await Render(form, ct); }
    }
    private async Task<IActionResult> Render(CheckoutForm form, CancellationToken ct)
    {
        try { await PrepareView(form, ct); return View("Index", form); }
        catch { return RedirectToAction("Index", "Shop"); }
    }
    private async Task PrepareView(CheckoutForm form, CancellationToken ct)
    {
        ViewBag.Addresses = await addresses.ListAsync(UserId, ct);
        ViewBag.Quote = await orders.QuoteAsync(form.ProductId, form.OfferId, UserId,
            form.Fulfillment, form.Province, form.Ward, ct);
        ViewBag.Product = await products.VisibleDetailsAsync(form.ProductId, UserId, User.IsInRole("Admin"), ct);
        ViewBag.Shipping = shipping;
        var mapUrl = configuration["Shipping:StoreMapUrl"];
        ViewBag.StoreMapUrl = Uri.TryCreate(mapUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.ToString() : null;
    }
}
