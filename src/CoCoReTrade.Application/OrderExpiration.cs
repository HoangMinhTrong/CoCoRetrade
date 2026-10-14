namespace CoCoReTrade.Application;

public sealed class OrderExpirationPolicy
{
    public int UnpaidReservationMinutes { get; }
    public OrderExpirationPolicy(int unpaidReservationMinutes = 1440)
    {
        if (unpaidReservationMinutes is < 15 or > 10_080)
            throw new ArgumentOutOfRangeException(nameof(unpaidReservationMinutes));
        UnpaidReservationMinutes = unpaidReservationMinutes;
    }
}

public interface IOrderExpirationService
{
    Task<int> ExpireUnpaidAsync(int batchSize, CancellationToken ct);
}

public sealed class OrderExpirationService(IOrderRepository repository, IOrderService orders,
    OrderExpirationPolicy policy, TimeProvider clock) : IOrderExpirationService
{
    public async Task<int> ExpireUnpaidAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var cutoff = clock.GetUtcNow().UtcDateTime.AddMinutes(-policy.UnpaidReservationMinutes);
        var ids = await repository.DueUnpaidOrdersAsync(cutoff, batchSize, ct);
        var expired = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await orders.CancelAsync(id, "system", true, ct);
                expired++;
            }
            catch (InvalidOperationException)
            {
                // The order may have been paid, prepared or cancelled since the batch was selected.
            }
        }
        return expired;
    }
}
