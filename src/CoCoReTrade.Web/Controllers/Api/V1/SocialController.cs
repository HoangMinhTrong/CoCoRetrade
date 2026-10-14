using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController, Authorize, Route("api/v1/social")]
public sealed class SocialController(ISocialService social) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("favorites")]
    public async Task<ActionResult<IReadOnlyList<ProductCard>>> Favorites(CancellationToken ct) =>
        Ok(await social.SavedProductsAsync(UserId, ct));
    [HttpPut("favorites/{productId:guid}")]
    public Task<ActionResult<object>> Like(Guid productId, CancellationToken ct) => SetFavorite(productId, true, ct);
    [HttpDelete("favorites/{productId:guid}")]
    public Task<ActionResult<object>> Unlike(Guid productId, CancellationToken ct) => SetFavorite(productId, false, ct);
    [HttpGet("following")]
    public async Task<ActionResult<IReadOnlyList<string>>> Following(CancellationToken ct) =>
        Ok(await social.FollowingIdsAsync(UserId, ct));
    [HttpPut("following/{sellerId}")]
    public Task<ActionResult<object>> Follow(string sellerId, CancellationToken ct) => SetFollow(sellerId, true, ct);
    [HttpDelete("following/{sellerId}")]
    public Task<ActionResult<object>> Unfollow(string sellerId, CancellationToken ct) => SetFollow(sellerId, false, ct);

    private async Task<ActionResult<object>> SetFavorite(Guid id, bool selected, CancellationToken ct)
    {
        try { return Ok(new { selected = await social.SetFavoriteAsync(UserId, id, selected, ct) }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
    private async Task<ActionResult<object>> SetFollow(string id, bool selected, CancellationToken ct)
    {
        try { return Ok(new { selected = await social.SetFollowAsync(UserId, id, selected, ct) }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
