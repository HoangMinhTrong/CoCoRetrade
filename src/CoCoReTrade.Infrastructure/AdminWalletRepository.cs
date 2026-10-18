using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AdminWalletRepository(AppDbContext db) : IAdminWalletRepository
{
    public async Task<IReadOnlyList<AdminWalletRow>> ListAsync(CancellationToken ct) =>
        await (from wallet in db.Wallets.AsNoTracking()
               join owner in db.Users.AsNoTracking() on wallet.UserId equals owner.Id
               orderby owner.FullName
               select new AdminWalletRow(wallet.Id, owner.Id, owner.FullName, owner.Email ?? "",
                   wallet.Balance, db.WalletTransactions.Count(x => x.WalletId == wallet.Id)))
            .Take(200).ToListAsync(ct);

    public async Task<AdminWalletDetails?> DetailsAsync(Guid id, CancellationToken ct)
    {
        var wallet = await (from w in db.Wallets.AsNoTracking()
                            join u in db.Users.AsNoTracking() on w.UserId equals u.Id
                            where w.Id == id
                            select new AdminWalletRow(w.Id, u.Id, u.FullName, u.Email ?? "", w.Balance,
                                db.WalletTransactions.Count(x => x.WalletId == w.Id))).FirstOrDefaultAsync(ct);
        if (wallet is null) return null;
        var transactions = await db.WalletTransactions.AsNoTracking().Where(x => x.WalletId == id)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200)
            .Select(x => new AdminWalletTransaction(x.Id, x.OrderId, x.Amount, x.Type,
                x.CreatedAtUtc, x.WithdrawalRequestId)).ToListAsync(ct);
        var withdrawals = await db.WithdrawalRequests.AsNoTracking().Where(x => x.WalletId == id)
            .Select(x => new AdminWithdrawalRow(x.Id, x.WalletId, wallet.OwnerName, x.Amount,
                x.BankAccountNumber ?? x.BankAccountMasked, x.Status, x.BankName,
                x.TransferredAtUtc, x.TransferReference)).ToListAsync(ct);
        return new AdminWalletDetails(wallet, transactions, withdrawals);
    }
    public async Task<IReadOnlyList<AdminWithdrawalRow>> WithdrawalsAsync(CancellationToken ct) =>
        await (from request in db.WithdrawalRequests.AsNoTracking()
               join wallet in db.Wallets.AsNoTracking() on request.WalletId equals wallet.Id
               join owner in db.Users.AsNoTracking() on wallet.UserId equals owner.Id
               orderby request.Status == "Pending" ? 0 : request.Status == "Approved" ? 1 : 2,
                   request.CreatedAtUtc descending
               select new AdminWithdrawalRow(request.Id, wallet.Id, owner.FullName, request.Amount,
                   request.BankAccountNumber ?? request.BankAccountMasked, request.Status,
                   request.BankName, request.TransferredAtUtc, request.TransferReference))
            .Take(200).ToListAsync(ct);

    public async Task<IReadOnlyList<SellerPayoutCandidate>> PayoutCandidatesAsync(CancellationToken ct) =>
        await (from order in db.Orders.AsNoTracking()
               join seller in db.Users.AsNoTracking() on order.SellerId equals seller.Id
               where order.Status == OrderStatus.Completed && order.PaymentStatus == PaymentStatus.Paid &&
                     !db.WalletTransactions.Any(x => x.OrderId == order.Id && x.Type == "SellerPayout")
               orderby order.CompletedAtUtc descending
               select new SellerPayoutCandidate(order.Id, order.ProductSku, seller.FullName,
                   order.ItemAmount, order.CommissionAmount, order.ItemAmount - order.CommissionAmount))
            .Take(200).ToListAsync(ct);
    public Task<Order?> OrderAsync(Guid id, CancellationToken ct) => db.Orders.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<Wallet?> WalletForUserAsync(string userId, CancellationToken ct) => db.Wallets.FirstOrDefaultAsync(x => x.UserId == userId, ct);
    public Task<bool> HasSellerPayoutAsync(Guid orderId, CancellationToken ct) =>
        db.WalletTransactions.AnyAsync(x => x.OrderId == orderId && x.Type == "SellerPayout", ct);
    public async Task<Guid> InTransactionAsync(Func<Task<Guid>> work, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await work();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException ex) { throw new InvalidOperationException("Ví đã thay đổi. Hãy tải lại trang và thử lại.", ex); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number is 2601 or 2627)
        { throw new InvalidOperationException("Tiền bán hàng của đơn này đã được ghi có.", ex); }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new InvalidOperationException("Ví vừa được cập nhật. Hãy tải lại trang và thử lại.", ex); }
    }
    public void AddWallet(Wallet wallet) => db.Wallets.Add(wallet);
    public void AddTransaction(WalletTransaction transaction) => db.WalletTransactions.Add(transaction);
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
