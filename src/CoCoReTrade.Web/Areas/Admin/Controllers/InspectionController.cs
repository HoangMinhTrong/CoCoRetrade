using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public sealed class InspectionController(IAdminProductService products) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet, Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<IActionResult> Index(string? search, CancellationToken ct)
    {
        ViewBag.Search = search;
        return View(await products.PendingAsync(ActorId, search, ct));
    }

    [HttpGet, Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<IActionResult> Review(Guid id, CancellationToken ct)
    {
        var detail = await products.GetAsync(ActorId, id, ct);
        if (detail is null || detail.Product.Status != ProductStatus.PendingApproval) return NotFound();
        ViewBag.Product = detail;
        return View(new InspectionForm
        {
            ProductId = id, ExpectedRowVersion = detail.RowVersion, Name = detail.Product.Name,
            Brand = detail.Product.Brand, Category = detail.Product.Category, Size = detail.Product.Size,
            OriginalPrice = detail.Product.OriginalPrice, AskingPrice = detail.Product.AskingPrice,
            VerifiedCondition = detail.Product.VerifiedCondition ?? detail.Product.DeclaredCondition
        });
    }

    [HttpPost, Authorize(Policy = StaffPermissions.Inspect), RequestSizeLimit(55_000_000)]
    public async Task<IActionResult> Record(Guid id, InspectionForm form, CancellationToken ct)
    {
        if (id != form.ProductId) return BadRequest();
        if (!ModelState.IsValid) return await ReviewWithErrors(id, form, ct);
        try
        {
            await products.InspectAsync(ActorId, id, form.ToRequest(), ct);
            TempData["Message"] = "Đã lưu kết quả kiểm định. Sản phẩm vẫn chờ phê duyệt.";
            return RedirectToAction(nameof(Review), new { id });
        }
        catch (Exception ex) when (ex is ProductConcurrencyException or DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Sản phẩm đã thay đổi. Trang kiểm định đã được tải lại; vui lòng kiểm tra thông tin trước khi lưu.";
            return RedirectToAction(nameof(Review), new { id });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await ReviewWithErrors(id, form, ct);
        }
    }

    [HttpPost, Authorize(Policy = StaffPermissions.Inspect)]
    public Task<IActionResult> Cover(Guid id, Guid imageId, string expectedRowVersion, CancellationToken ct) =>
        Decide(id, () => products.SelectCoverAsync(ActorId, id, imageId, expectedRowVersion, ct),
            "Đã chọn ảnh bìa cho sản phẩm.");

    [HttpPost, Authorize(Policy = StaffPermissions.Publish)]
    public Task<IActionResult> Publish(Guid id, ProductDecisionForm form, CancellationToken ct) =>
        Decide(id, () => products.PublishAsync(ActorId, id, form.ExpectedRowVersion,
            form.ConfirmedSku, form.PhysicalItemConfirmed, ct), "Đã đăng bán sản phẩm.");

    [HttpPost, Authorize(Policy = StaffPermissions.Reject)]
    public Task<IActionResult> Reject(Guid id, ProductDecisionForm form, CancellationToken ct) =>
        Decide(id, () => products.RejectAsync(ActorId, id, form.Reason, form.ExpectedRowVersion, ct), "Đã từ chối sản phẩm.");

    [HttpGet, Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<IActionResult> Label(Guid id, CancellationToken ct)
    {
        var detail = await products.GetAsync(ActorId, id, ct);
        return detail is null || string.IsNullOrWhiteSpace(detail.Product.Sku) ? NotFound() : View(detail);
    }

    [HttpGet, Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var detail = await products.GetAsync(ActorId, id, ct);
        return detail is null ? NotFound() : View(detail);
    }

    private async Task<IActionResult> ReviewWithErrors(Guid id, InspectionForm form, CancellationToken ct)
    {
        var detail = await products.GetAsync(ActorId, id, ct);
        if (detail is null) return NotFound();
        ViewBag.Product = detail;
        return View("Review", form);
    }

    private async Task<IActionResult> Decide(Guid id, Func<Task<AdminProductDetails>> action, string message)
    {
        try { await action(); TempData["Message"] = message; return RedirectToAction(nameof(Index)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) when (ex is ProductConcurrencyException or DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Sản phẩm đã thay đổi. Hãy xem lại thông tin mới trước khi tiếp tục.";
            return RedirectToAction(nameof(Review), new { id });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Review), new { id });
        }
    }
}
