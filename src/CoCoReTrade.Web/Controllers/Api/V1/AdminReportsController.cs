using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize(Policy = StaffPermissions.ManageWallets)]
[Route("api/v1/admin/reports")]
public sealed class AdminReportsController(IAdminFinancialReportService reports) : ControllerBase
{
    [HttpGet("revenue")]
    public async Task<ActionResult<AdminFinancialReport>> Revenue(
        string period = "month", DateOnly? date = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await reports.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                period, date, ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
