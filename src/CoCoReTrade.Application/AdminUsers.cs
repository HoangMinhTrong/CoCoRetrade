namespace CoCoReTrade.Application;

public sealed record AdminUserRow(string Id, string FullName, string Email, string? Phone,
    bool EmailConfirmed, bool IsInspector, bool IsAdmin, bool HasDirectPublishGrant, bool IsLocked,
    int TrustScore = 100, bool TrustSuspended = false);

public sealed class TrustPolicy
{
    public int MinimumScore { get; }
    public TrustPolicy(int minimumScore = 30)
    {
        if (minimumScore is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(minimumScore));
        MinimumScore = minimumScore;
    }
}

public interface IAdminUserRepository
{
    Task<IReadOnlyList<AdminUserRow>> SearchAsync(string? search, CancellationToken ct);
    Task<AdminUserRow?> FindAsync(string id, CancellationToken ct);
    Task SetInspectorAsync(string targetId, bool enabled, string actorId, DateTime nowUtc, CancellationToken ct);
    Task SetPublishGrantAsync(string targetId, bool enabled, string actorId, DateTime nowUtc, CancellationToken ct);
    Task SetLockedAsync(string targetId, bool locked, string actorId, DateTime nowUtc, CancellationToken ct);
    Task SetTrustScoreAsync(string targetId, int score, int minimumScore, string reason,
        string actorId, DateTime nowUtc, CancellationToken ct);
}

public interface IAdminUserService
{
    Task<IReadOnlyList<AdminUserRow>> SearchAsync(string actorId, string? search, CancellationToken ct);
    Task<AdminUserRow> SetInspectorAsync(string actorId, string targetId, bool enabled, CancellationToken ct);
    Task<AdminUserRow> SetPublishGrantAsync(string actorId, string targetId, bool enabled, CancellationToken ct);
    Task<AdminUserRow> SetLockedAsync(string actorId, string targetId, bool locked, CancellationToken ct);
    Task<AdminUserRow> SetTrustScoreAsync(string actorId, string targetId, int score,
        string reason, CancellationToken ct);
}

public sealed class AdminUserService(IAdminUserRepository repository, IStaffPermissionChecker permissions,
    TimeProvider clock, TrustPolicy trust) : IAdminUserService
{
    public async Task<IReadOnlyList<AdminUserRow>> SearchAsync(string actorId, string? search, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageUsers, ct);
        return await repository.SearchAsync(search, ct);
    }
    public async Task<AdminUserRow> SetInspectorAsync(string actorId, string targetId, bool enabled, CancellationToken ct)
    {
        await EnsureTarget(actorId, targetId, ct);
        await repository.SetInspectorAsync(targetId, enabled, actorId, clock.GetUtcNow().UtcDateTime, ct);
        return (await repository.FindAsync(targetId, ct))!;
    }
    public async Task<AdminUserRow> SetPublishGrantAsync(string actorId, string targetId, bool enabled, CancellationToken ct)
    {
        var target = await EnsureTarget(actorId, targetId, ct);
        if (!target.IsInspector) throw new InvalidOperationException("Only inspectors can receive a publish grant.");
        await repository.SetPublishGrantAsync(targetId, enabled, actorId, clock.GetUtcNow().UtcDateTime, ct);
        return (await repository.FindAsync(targetId, ct))!;
    }
    public async Task<AdminUserRow> SetLockedAsync(string actorId, string targetId, bool locked, CancellationToken ct)
    {
        await EnsureTarget(actorId, targetId, ct);
        await repository.SetLockedAsync(targetId, locked, actorId, clock.GetUtcNow().UtcDateTime, ct);
        return (await repository.FindAsync(targetId, ct))!;
    }
    public async Task<AdminUserRow> SetTrustScoreAsync(string actorId, string targetId, int score,
        string reason, CancellationToken ct)
    {
        await EnsureTarget(actorId, targetId, ct);
        if (score is < 0 or > 100 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500)
            throw new ArgumentException("Điểm tín nhiệm phải từ 0 đến 100 và cần ghi lý do ít nhất 5 ký tự.");
        await repository.SetTrustScoreAsync(targetId, score, trust.MinimumScore, reason.Trim(),
            actorId, clock.GetUtcNow().UtcDateTime, ct);
        return (await repository.FindAsync(targetId, ct))!;
    }
    private async Task<AdminUserRow> EnsureTarget(string actorId, string targetId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageUsers, ct);
        if (actorId == targetId) throw new InvalidOperationException("You cannot change your own staff access here.");
        var target = await repository.FindAsync(targetId, ct) ?? throw new KeyNotFoundException("User not found.");
        if (target.IsAdmin) throw new InvalidOperationException("Admin accounts cannot be changed here.");
        return target;
    }
}
