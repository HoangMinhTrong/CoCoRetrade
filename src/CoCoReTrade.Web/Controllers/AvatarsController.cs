using CoCoReTrade.Application;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers;

public sealed class AvatarsController(IUserProfileService profiles) : Controller
{
    [HttpGet("/avatars/{userId}")]
    public async Task<IActionResult> Get(string userId, CancellationToken ct)
    {
        var avatar = await profiles.OpenAvatarAsync(userId, ct);
        Response.Headers.CacheControl = "no-cache";
        return avatar is null ? NotFound() : File(avatar.Value.Stream, avatar.Value.ContentType);
    }
}
