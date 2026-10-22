using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class PlatformCommissionRepository(AppDbContext db) : IPlatformCommissionRepository
{
    public Task<bool> HasEntryAsync(Guid orderId, CancellationToken ct) =>
        db.PlatformCommissionEntries.AnyAsync(x => x.OrderId == orderId, ct);

    public void Add(PlatformCommissionEntry entry) => db.PlatformCommissionEntries.Add(entry);

    public Task<bool> IsAdminAsync(string userId, CancellationToken ct) =>
        db.UserRoles.Join(db.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .AnyAsync(x => x.UserId == userId && x.Name == "Admin", ct);

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new InvalidOperationException("Có yêu cầu rút tiền đồng thời. Vui lòng tải lại ví và thử lại.", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 1205 or 2601 or 2627)
        { throw new InvalidOperationException("Yêu cầu rút tiền đã được xử lý hoặc có xung đột. Vui lòng tải lại ví.", ex); }
    }

    public Task<PlatformCommissionWithdrawal?> ByRequestKeyAsync(string adminId, Guid requestKey, CancellationToken ct) =>
        db.PlatformCommissionWithdrawals.FirstOrDefaultAsync(x => x.AdminId == adminId && x.RequestKey == requestKey, ct);

    public Task<AdminPayoutAccount?> PayoutAccountAsync(string adminId, CancellationToken ct) =>
        db.AdminPayoutAccounts.FirstOrDefaultAsync(x => x.AdminId == adminId, ct);

    public async Task<decimal> AvailableAsync(CancellationToken ct) =>
        (await db.PlatformCommissionEntries.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m) -
        (await db.PlatformCommissionWithdrawals.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m);

    public void AddWithdrawal(PlatformCommissionWithdrawal withdrawal) => db.PlatformCommissionWithdrawals.Add(withdrawal);
    public void AddPayoutAccount(AdminPayoutAccount account) => db.AdminPayoutAccounts.Add(account);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<PlatformCommissionWallet> SnapshotAsync(string adminId, CancellationToken ct)
    {
        var entries = db.PlatformCommissionEntries.AsNoTracking();
        var total = await entries.Select(x => (decimal?)x.Amount).SumAsync(ct) ?? 0m;
        var withdrawn = await db.PlatformCommissionWithdrawals.AsNoTracking()
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var count = await entries.CountAsync(ct);
        var saved = await db.AdminPayoutAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.AdminId == adminId, ct);
        var recent = await (from entry in entries
                            join order in db.Orders.AsNoTracking() on entry.OrderId equals order.Id
                            orderby entry.CreatedAtUtc descending
                            select new PlatformCommissionRow(entry.OrderId, order.ProductSku,
                                order.ItemAmount, entry.Amount, entry.CreatedAtUtc))
            .Take(100).ToListAsync(ct);
        var withdrawals = await (from withdrawal in db.PlatformCommissionWithdrawals.AsNoTracking()
                                 join admin in db.Users.AsNoTracking() on withdrawal.AdminId equals admin.Id
                                 orderby withdrawal.CreatedAtUtc descending
                                 select new { withdrawal.Id, withdrawal.Amount, AdminName = admin.FullName,
                                     withdrawal.BankName, withdrawal.BankAccountNumber, withdrawal.CreatedAtUtc })
            .Take(100).ToListAsync(ct);
        return new PlatformCommissionWallet(total - withdrawn, count, recent, total, withdrawn,
            saved?.BankAccountNumber, saved?.BankName,
            withdrawals.Select(x => new PlatformWithdrawalRow(x.Id, x.Amount, x.AdminName, x.BankName,
                "****" + x.BankAccountNumber[^4..], x.CreatedAtUtc)).ToList());
    }
}
