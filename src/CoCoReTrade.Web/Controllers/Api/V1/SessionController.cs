using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Route("api/v1/session")]
public sealed class SessionController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("antiforgery")]
    [ResponseCache(NoStore = true)]
    public IActionResult Token()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}
