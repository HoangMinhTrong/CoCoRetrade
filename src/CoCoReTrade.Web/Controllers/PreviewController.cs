using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

// Read-only screens for reviewing Razor UI before SQL Server is installed.
[Route("preview")]
public sealed class PreviewController(IConfiguration configuration, IWebHostEnvironment environment,
    IProductService products, MarketplacePolicy policy, ShippingPolicy shipping) : Controller
{
    private bool Enabled => environment.IsDevelopment() && configuration.GetValue<bool>("Preview:Enabled");
    private static readonly Guid ProductId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OfferId = Guid.Parse("22222222-2222-2222-2222-000000000001");

    [HttpGet("")]
    public IActionResult Index() => Enabled ? View() : NotFound();

    [HttpGet("make-offer")]
    public async Task<IActionResult> MakeOffer(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var product = (await products.VisibleDetailsAsync(ProductId, null, false, ct))!;
        ViewBag.Product = product;
        ViewBag.MinimumPrice = policy.MinimumOffer(product.AskingPrice);
        return View("~/Views/Offers/Make.cshtml", new MakeOfferForm { ProductId = ProductId });
    }

    [HttpGet("offer")]
    public IActionResult Offer()
    {
        if (!Enabled) return NotFound();
        var now = DateTime.UtcNow;
        ViewBag.PreviewMode = true;
        var offer = new OfferDto(OfferId, ProductId, "CC-2026-000001", "Áo khoác denim vintage",
            "preview-buyer", "preview-seller", 320_000, 275_000, 275_000, OfferStatus.Accepted, 2,
            now.AddHours(-3), now.AddHours(21),
            [new OfferEventDto(OfferEventAction.Submitted, "preview-buyer", 240_000, now.AddHours(-3)),
             new OfferEventDto(OfferEventAction.SellerCountered, "preview-seller", 290_000, now.AddHours(-2)),
             new OfferEventDto(OfferEventAction.BuyerCountered, "preview-buyer", 275_000, now.AddHours(-1)),
             new OfferEventDto(OfferEventAction.Accepted, "preview-seller", 275_000, now)]);
        return View("~/Views/Offers/Details.cshtml", offer);
    }

    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        ViewBag.PreviewMode = true;
        ViewBag.Quote = new CheckoutQuote(ProductId, OfferId, "CC-2026-000001", "Áo khoác denim vintage",
            "M", 275_000, 0, 275_000, 41_250, DateTime.UtcNow.AddHours(21));
        ViewBag.Product = await products.VisibleDetailsAsync(ProductId, null, false, ct);
        ViewBag.Shipping = shipping;
        ViewBag.Addresses = Array.Empty<AddressDto>();
        return View("~/Views/Checkout/Index.cshtml", new CheckoutForm { ProductId = ProductId, OfferId = OfferId,
            Fulfillment = FulfillmentType.Shipping });
    }
}
