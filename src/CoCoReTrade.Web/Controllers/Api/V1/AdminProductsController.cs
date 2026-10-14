using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/admin/products")]
public sealed class AdminProductsController(IAdminProductService products) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("dashboard"), Authorize(Policy = StaffPermissions.ReadPending)]
    public async Task<ActionResult<AdminProductDashboard>> Dashboard(CancellationToken ct) =>
        Ok(await products.DashboardAsync(ActorId, ct));
    [HttpGet, Authorize(Policy = StaffPermissions.ManageProducts)]
    public async Task<ActionResult<IReadOnlyList<AdminProductItem>>> Index(ProductStatus? status, string? search, CancellationToken ct) =>
        Ok(await products.ManageAsync(ActorId, status, search, ct));
    [HttpGet("audit"), Authorize(Policy = StaffPermissions.ReadAudit)]
    public async Task<ActionResult<IReadOnlyList<AuditEntry>>> Audit(Guid? productId, CancellationToken ct) =>
        Ok(await products.AuditAsync(ActorId, productId, ct));
}
