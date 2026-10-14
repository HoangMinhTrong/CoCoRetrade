using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = StaffPermissions.ManageProducts)]
public sealed class ProductsController(IAdminProductService products) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(ProductStatus? status, string? search, CancellationToken ct)
    {
        ViewBag.Status = status;
        ViewBag.Search = search;
        return View(await products.ManageAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, status, search, ct));
    }
}
