using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController, Authorize, Route("api/v1/mobile/profile")]
public sealed class MobileProfileController(IUserProfileService profiles) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> Get(CancellationToken ct) =>
        Ok(await profiles.GetAsync(UserId, ct));

    [HttpPost("avatar"), Consumes("multipart/form-data"), RequestSizeLimit(6_000_000)]
    public async Task<ActionResult<UserProfileDto>> Avatar([FromForm] IFormFile image, CancellationToken ct)
    {
        if (image is null) return BadRequest(new { error = "Hãy chọn ảnh đại diện." });
        try
        {
            await profiles.SetAvatarAsync(UserId,
                new ImageUpload(image.FileName, image.ContentType, image.Length, image.OpenReadStream), ct);
            return Ok(await profiles.GetAsync(UserId, ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
