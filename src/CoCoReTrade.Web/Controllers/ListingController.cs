using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            TempData["Message"] = $"Đã gửi ký gửi {product.ConsignmentCode} để CoCo duyệt sơ bộ. Bạn có thể sửa thông tin trước khi được duyệt.";
            return RedirectToAction(nameof(Closet));
        }
        catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View(form); }
    }
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var product = await products.VisibleDetailsAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), false, ct);
        if (product is null || product.SellerId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return NotFound();
        if (product.Status != ProductStatus.PendingApproval || product.PreliminaryStatus == PreliminaryReviewStatus.Approved)
            return RedirectToAction(nameof(Closet));
        ViewBag.Product = product;
        return View(new ListingForm
        {
            Name = product.Name, Brand = product.Brand, Category = product.Category, Size = product.Size,
            OriginalPrice = product.OriginalPrice, AskingPrice = product.AskingPrice,
            ConditionPercent = product.DeclaredCondition, AllowOffers = product.AllowOffers,
            RetainedImageIds = product.ImageIds.ToList(), ExpectedRowVersion = product.RowVersion
        });
    }
    [HttpPost, RequestSizeLimit(55_000_000)]
    public async Task<IActionResult> Edit(Guid id, ListingForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await EditWithErrors(id, form, ct);
        try
        {
            await products.EditAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, id, form.ToEditRequest(), ct);
            TempData["Message"] = "Đã cập nhật sản phẩm và gửi lại để CoCo duyệt sơ bộ.";
            return RedirectToAction(nameof(Closet));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await EditWithErrors(id, form, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Sản phẩm vừa được cập nhật hoặc duyệt sơ bộ. Hãy kiểm tra trạng thái mới trước khi sửa.";
            return RedirectToAction(nameof(Closet));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    private async Task<IActionResult> EditWithErrors(Guid id, ListingForm form, CancellationToken ct)
    {
        var product = await products.VisibleDetailsAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), false, ct);
        if (product is null || product.SellerId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return NotFound();
        if (product.Status != ProductStatus.PendingApproval || product.PreliminaryStatus == PreliminaryReviewStatus.Approved)
            return RedirectToAction(nameof(Closet));
        ViewBag.Product = product;
        return View("Edit", form);
    }
    [HttpGet]
    public async Task<IActionResult> Closet(CancellationToken ct)
    {
        ViewBag.IsCloset = true;
        return View(await products.ClosetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
    }
}
