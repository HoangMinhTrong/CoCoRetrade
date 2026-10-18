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

public interface IAcceptedPickupExpirationRepository
{
    Task<IReadOnlyList<Guid>> DueAcceptedPickupOrdersAsync(DateTime nowUtc, int batchSize, CancellationToken ct);
}

public sealed class OrderExpirationService(IOrderRepository repository, IOrderService orders,
    OrderExpirationPolicy policy, TimeProvider clock,
    IAcceptedPickupExpirationRepository? acceptedPickups = null) : IOrderExpirationService
{
    public async Task<int> ExpireUnpaidAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddMinutes(-policy.UnpaidReservationMinutes);
        var ids = await repository.DueUnpaidOrdersAsync(cutoff, batchSize, ct);
        IReadOnlyList<Guid> duePickups = acceptedPickups is null ? Array.Empty<Guid>() :
            await acceptedPickups.DueAcceptedPickupOrdersAsync(now, batchSize, ct);
        var expired = 0;
        foreach (var id in duePickups.Concat(ids).Distinct().Take(batchSize))
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
