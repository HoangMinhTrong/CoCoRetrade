using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record FinancialSalesTotals(int CompletedOrderCount, decimal CommissionAmount,
    decimal SellerNetAmount, decimal SellerWalletCreditedAmount);

public sealed record AdminFinancialReport(string Period, DateOnly FromDate, DateOnly ThroughDate,
    int CompletedOrderCount, decimal CommissionAmount, decimal SellerNetAmount,
    decimal SellerWalletCreditedAmount)
{
    public decimal SellerWalletPendingAmount => Math.Max(0, SellerNetAmount - SellerWalletCreditedAmount);
}

public interface IAdminFinancialReportRepository
{
    Task<FinancialSalesTotals> SalesTotalsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}

public interface IAdminFinancialReportService
{
    Task<AdminFinancialReport> GetAsync(string actorId, string period, DateOnly? referenceDate,
        CancellationToken ct);
}

public sealed class AdminFinancialReportService(IAdminFinancialReportRepository repository,
    IStaffPermissionChecker permissions, TimeProvider clock) : IAdminFinancialReportService
{
    public async Task<AdminFinancialReport> GetAsync(string actorId, string period,
        DateOnly? referenceDate, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageWallets, ct);
        var zone = VietnamTimeZone();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var anchor = referenceDate ?? today;
        var key = period.Trim().ToLowerInvariant();
        var (from, to) = key switch
        {
            "day" => (anchor, anchor.AddDays(1)),
            "week" => WeekBounds(anchor),
            "month" => MonthBounds(anchor),
            _ => throw new ArgumentException("Chọn khoảng thời gian theo ngày, tuần hoặc tháng.", nameof(period))
        };

        var totals = await repository.SalesTotalsAsync(ToUtc(from, zone), ToUtc(to, zone), ct);
        var label = key switch { "day" => "Ngày", "week" => "Tuần", _ => "Tháng" };
        return new AdminFinancialReport(label, from, to.AddDays(-1), totals.CompletedOrderCount,
            totals.CommissionAmount, totals.SellerNetAmount, totals.SellerWalletCreditedAmount);
    }

    private DateTime ToUtc(DateOnly date, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);

    private static (DateOnly From, DateOnly To) WeekBounds(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.AddDays(-daysSinceMonday);
        return (monday, monday.AddDays(7));
    }

    private static (DateOnly From, DateOnly To) MonthBounds(DateOnly date)
    {
        var first = new DateOnly(date.Year, date.Month, 1);
        return (first, first.AddMonths(1));
    }

    private static TimeZoneInfo VietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }
}
