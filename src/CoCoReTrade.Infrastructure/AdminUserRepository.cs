using System.Security.Claims;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AdminUserRepository(AppDbContext db, UserManager<ApplicationUser> users) : IAdminUserRepository
{
    public async Task<IReadOnlyList<AdminUserRow>> SearchAsync(string? search, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.FullName.Contains(term) || x.Email!.Contains(term) || x.PhoneNumber!.Contains(term));
        }
        var found = await query.OrderBy(x => x.FullName).Take(100).ToListAsync(ct);
        var rows = new List<AdminUserRow>();
        foreach (var user in found) rows.Add(await ToRow(user));
        return rows;
    }
    public async Task<AdminUserRow?> FindAsync(string id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? null : await ToRow(user);
    }
    public Task SetInspectorAsync(string targetId, bool enabled, string actorId, DateTime nowUtc, CancellationToken ct) =>
        Change(targetId, actorId, nowUtc, enabled ? "Users.InspectorGranted" : "Users.InspectorRevoked", async user =>
        {
            var has = await users.IsInRoleAsync(user, "Inspector");
            if (enabled && !has)
            {
                await Ensure(await users.AddToRoleAsync(user, "Inspector"));
                if (!await users.IsInRoleAsync(user, "User")) await Ensure(await users.AddToRoleAsync(user, "User"));
            }
            else if (!enabled && has)
            {
                await Ensure(await users.RemoveFromRoleAsync(user, "Inspector"));
                await RemovePublishClaim(user);
            }
        }, ct);
    public Task SetPublishGrantAsync(string targetId, bool enabled, string actorId, DateTime nowUtc, CancellationToken ct) =>
        Change(targetId, actorId, nowUtc, enabled ? "Users.PublishGranted" : "Users.PublishRevoked", async user =>
        {
            if (!await users.IsInRoleAsync(user, "Inspector")) throw new InvalidOperationException("Người dùng này không phải kiểm định viên.");
            var has = (await users.GetClaimsAsync(user)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == StaffPermissions.Publish);
            if (enabled && !has) await Ensure(await users.AddClaimAsync(user, new Claim(StaffPermissions.ClaimType, StaffPermissions.Publish)));
            if (!enabled && has) await RemovePublishClaim(user);
        }, ct);
    public Task SetLockedAsync(string targetId, bool locked, string actorId, DateTime nowUtc, CancellationToken ct) =>
        Change(targetId, actorId, nowUtc, locked ? "Users.Locked" : "Users.Unlocked", async user =>
        {
            await Ensure(await users.SetLockoutEnabledAsync(user, true));
            await Ensure(await users.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null));
        }, ct);
    public Task SetTrustScoreAsync(string targetId, int score, int minimumScore, string reason,
        string actorId, DateTime nowUtc, CancellationToken ct) =>
        Change(targetId, actorId, nowUtc, "Users.TrustScoreChanged", async user =>
        {
            user.TrustScore = score;
            user.TrustSuspended = score < minimumScore;
            await Ensure(await users.UpdateAsync(user));
        }, ct, $"Điểm tín nhiệm={score}; tạm khóa={(score < minimumScore ? "có" : "không")}; lý do={reason}");

    private async Task Change(string targetId, string actorId, DateTime nowUtc, string action,
        Func<ApplicationUser, Task> work, CancellationToken ct, string? details = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await users.FindByIdAsync(targetId) ?? throw new KeyNotFoundException("Không tìm thấy người dùng.");
        if (await users.IsInRoleAsync(user, "Admin")) throw new InvalidOperationException("Không thể thay đổi tài khoản quản trị tại đây.");
        await work(user);
        await Ensure(await users.UpdateSecurityStampAsync(user));
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), ActorId = actorId, Action = action,
            EntityType = "User", EntityId = targetId, Details = details ?? action, CreatedAtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private async Task RemovePublishClaim(ApplicationUser user)
    {
        foreach (var claim in (await users.GetClaimsAsync(user)).Where(x => x.Type == StaffPermissions.ClaimType && x.Value == StaffPermissions.Publish))
            await Ensure(await users.RemoveClaimAsync(user, claim));
    }
    private static Task Ensure(IdentityResult outcome)
    {
        if (!outcome.Succeeded) throw new InvalidOperationException(string.Join("; ", outcome.Errors.Select(x => x.Description)));
        return Task.CompletedTask;
    }
    private async Task<AdminUserRow> ToRow(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var direct = (await users.GetClaimsAsync(user)).Any(x => x.Type == StaffPermissions.ClaimType && x.Value == StaffPermissions.Publish);
        return new AdminUserRow(user.Id, user.FullName, user.Email ?? "", user.PhoneNumber, user.EmailConfirmed,
            roles.Contains("Inspector"), roles.Contains("Admin"), direct, user.LockoutEnd > DateTimeOffset.UtcNow,
            user.TrustScore, user.TrustSuspended);
    }
}
