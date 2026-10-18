using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AdminFinancialReportRepository(AppDbContext db) : IAdminFinancialReportRepository
{
    public async Task<FinancialSalesTotals> SalesTotalsAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken ct)
    {
        var sales = db.Orders.AsNoTracking().Where(order =>
            order.Status == OrderStatus.Completed && order.PaymentStatus == PaymentStatus.Paid &&
            order.CompletedAtUtc >= fromUtc && order.CompletedAtUtc < toUtc);

        var totals = await sales.GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Commission = group.Sum(order => order.CommissionAmount),
                SellerNet = group.Sum(order => order.ItemAmount - order.CommissionAmount)
            })
            .FirstOrDefaultAsync(ct);

        var walletCredited = await db.WalletTransactions.AsNoTracking()
            .Where(transaction => transaction.Type == "SellerPayout" && transaction.OrderId.HasValue &&
                sales.Any(order => order.Id == transaction.OrderId!.Value))
            .SumAsync(transaction => (decimal?)transaction.Amount, ct) ?? 0;

        return new FinancialSalesTotals(totals?.Count ?? 0, totals?.Commission ?? 0,
            totals?.SellerNet ?? 0, walletCredited);
    }
}
