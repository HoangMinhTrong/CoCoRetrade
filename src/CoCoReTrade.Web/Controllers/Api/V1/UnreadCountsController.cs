using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/unread-counts")]
public sealed class UnreadCountsController(IUnreadCountsService counts) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<UnreadCountsDto>> Get(CancellationToken ct) =>
        Ok(await counts.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
}
