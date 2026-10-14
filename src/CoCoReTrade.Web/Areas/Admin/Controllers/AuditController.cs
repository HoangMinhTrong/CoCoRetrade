using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = StaffPermissions.ReadAudit)]
public sealed class AuditController(IAdminProductService products) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid? productId, CancellationToken ct)
    {
        ViewBag.ProductId = productId;
        return View(await products.AuditAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, productId, ct));
    }
}
