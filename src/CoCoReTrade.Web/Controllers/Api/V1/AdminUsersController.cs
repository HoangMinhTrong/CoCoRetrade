using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record ToggleAdminUserRequest(bool Enabled);

[ApiController]
[Authorize(Policy = StaffPermissions.ManageUsers)]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(IAdminUserService users) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserRow>>> Index(string? search, CancellationToken ct) =>
        Ok(await users.SearchAsync(ActorId, search, ct));
    [HttpPost("{id}/inspector")]
    public Task<ActionResult<AdminUserRow>> Inspector(string id, [FromBody] ToggleAdminUserRequest request, CancellationToken ct) =>
        Change(() => users.SetInspectorAsync(ActorId, id, request.Enabled, ct));
    [HttpPost("{id}/publish-grant")]
    public Task<ActionResult<AdminUserRow>> PublishGrant(string id, [FromBody] ToggleAdminUserRequest request, CancellationToken ct) =>
        Change(() => users.SetPublishGrantAsync(ActorId, id, request.Enabled, ct));
    [HttpPost("{id}/lock")]
    public Task<ActionResult<AdminUserRow>> Lock(string id, [FromBody] ToggleAdminUserRequest request, CancellationToken ct) =>
        Change(() => users.SetLockedAsync(ActorId, id, request.Enabled, ct));
    private async Task<ActionResult<AdminUserRow>> Change(Func<Task<AdminUserRow>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
