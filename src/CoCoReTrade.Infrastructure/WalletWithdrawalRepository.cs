using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class WalletWithdrawalRepository(AppDbContext db) : IWalletWithdrawalRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        { throw new InvalidOperationException("Wallet changed concurrently. Reload and retry.", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        { throw new InvalidOperationException("Withdrawal request was already recorded.", ex); }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new InvalidOperationException("Concurrent wallet update. Retry the request.", ex); }
    }
    public Task<Wallet?> WalletForUserAsync(string userId, CancellationToken ct) =>
        db.Wallets.FirstOrDefaultAsync(x => x.UserId == userId, ct);
    public Task<Wallet?> WalletByIdAsync(Guid id, CancellationToken ct) =>
        db.Wallets.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<WithdrawalRequest?> WithdrawalAsync(Guid id, CancellationToken ct) =>
        db.WithdrawalRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<WithdrawalRequest?> ByRequestKeyAsync(Guid walletId, Guid key, CancellationToken ct) =>
        db.WithdrawalRequests.FirstOrDefaultAsync(x => x.WalletId == walletId && x.RequestKey == key, ct);
    public async Task<WalletBalanceDto> BalanceAsync(string userId, CancellationToken ct)
    {
        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var pendingSeller = await db.Orders.AsNoTracking()
            .Where(x => x.SellerId == userId && x.PaymentStatus == PaymentStatus.Paid &&
                x.Status == OrderStatus.Completed &&
                !db.WalletTransactions.Any(t => t.OrderId == x.Id && t.Type == "SellerPayout"))
            .SumAsync(x => (decimal?)(x.ItemAmount - x.CommissionAmount), ct) ?? 0;
        if (wallet is null) return new WalletBalanceDto(pendingSeller, 0, 0, 0);
        var reserved = await db.WithdrawalRequests.AsNoTracking()
            .Where(x => x.WalletId == wallet.Id && (x.Status == "Pending" || x.Status == "Approved"))
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var withdrawn = await db.WalletTransactions.AsNoTracking()
            .Where(x => x.WalletId == wallet.Id && x.Type == "WithdrawalSettled")
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        return new WalletBalanceDto(pendingSeller, wallet.Balance, reserved, withdrawn);
    }
    public async Task<IReadOnlyList<WithdrawalRequest>> ForUserWithdrawalsAsync(string userId, CancellationToken ct) =>
        await db.WithdrawalRequests.AsNoTracking()
            .Where(x => db.Wallets.Any(w => w.Id == x.WalletId && w.UserId == userId))
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
    public async Task<IReadOnlyList<string>> AdminUserIdsAsync(CancellationToken ct) =>
        await db.UserRoles.Join(db.Roles, ur => ur.RoleId, role => role.Id,
                (ur, role) => new { ur.UserId, role.Name })
            .Where(x => x.Name == "Admin").Select(x => x.UserId).Distinct().ToListAsync(ct);
    public void AddWithdrawal(WithdrawalRequest request) => db.WithdrawalRequests.Add(request);
    public void AddTransaction(WalletTransaction transaction) => db.WalletTransactions.Add(transaction);
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
