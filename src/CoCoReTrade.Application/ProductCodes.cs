namespace CoCoReTrade.Application;

public interface IProductCodeSequence
{
    Task<long> NextAsync(CancellationToken ct);
}

public interface IProductCodeService
{
    Task<string> NewConsignmentCodeAsync(CancellationToken ct);
    Task<string> NewInventorySkuAsync(CancellationToken ct);
}

public sealed class ProductCodeService(IProductCodeSequence sequence, TimeProvider clock) : IProductCodeService
{
    public Task<string> NewConsignmentCodeAsync(CancellationToken ct) => NextAsync("CG", ct);
    public Task<string> NewInventorySkuAsync(CancellationToken ct) => NextAsync("CC", ct);

    private async Task<string> NextAsync(string prefix, CancellationToken ct)
    {
        var value = await sequence.NextAsync(ct);
        if (value is < 1 or > 999_999)
            throw new InvalidOperationException("Product code sequence is exhausted.");
        return $"{prefix}-{clock.GetUtcNow():yyyy}-{value:D6}";
    }
}
