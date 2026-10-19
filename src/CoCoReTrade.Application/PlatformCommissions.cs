using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record PlatformCommissionRow(Guid OrderId, string ProductSku, decimal MerchandiseAmount,
    decimal CommissionAmount, DateTime CreatedAtUtc);

public sealed record PlatformCommissionWallet(decimal Balance, int OrderCount,
    IReadOnlyList<PlatformCommissionRow> RecentEntries);

public interface IPlatformCommissionRepository
{
    Task<bool> HasEntryAsync(Guid orderId, CancellationToken ct);
    void Add(PlatformCommissionEntry entry);
    Task<PlatformCommissionWallet> SnapshotAsync(CancellationToken ct);
}

public interface IPlatformCommissionService
{
    Task<PlatformCommissionWallet> GetAsync(string actorId, CancellationToken ct);
}

public sealed class PlatformCommissionService(IPlatformCommissionRepository repository,
    IStaffPermissionChecker permissions) : IPlatformCommissionService
{
    public async Task<PlatformCommissionWallet> GetAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        return await repository.SnapshotAsync(ct);
    }
}
