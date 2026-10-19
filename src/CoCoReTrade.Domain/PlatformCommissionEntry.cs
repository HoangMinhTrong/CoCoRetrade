namespace CoCoReTrade.Domain;

// An immutable receipt for CoCo's share of a completed, paid order.
// The platform balance is the sum of these receipts, not an admin user's wallet.
public sealed class PlatformCommissionEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PlatformCommissionEntry() { }

    public PlatformCommissionEntry(Order order, DateTime nowUtc)
    {
        if (order.Status != OrderStatus.Completed || order.PaymentStatus != PaymentStatus.Paid)
            throw new InvalidOperationException("Chỉ ghi nhận hoa hồng khi đơn đã thanh toán và hoàn tất.");
        if (order.CommissionAmount < 0 || order.CommissionAmount >= order.ItemAmount ||
            order.CommissionAmount != decimal.Truncate(order.CommissionAmount))
            throw new InvalidOperationException("Hoa hồng của đơn hàng không hợp lệ.");
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Thời gian phải là UTC.", nameof(nowUtc));

        OrderId = order.Id;
        Amount = order.CommissionAmount;
        CreatedAtUtc = nowUtc;
    }
}
