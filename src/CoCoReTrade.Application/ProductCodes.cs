namespace CoCoReTrade.Application;

using System.Globalization;

public interface IProductCodeSequence
{
    Task<long> NextAsync(CancellationToken ct);
}

public interface IProductCodeService
{
    Task<string> NewConsignmentCodeAsync(CancellationToken ct);
    Task<string> NewProductSkuAsync(CancellationToken ct);
}

public sealed class ProductCodeService(IProductCodeSequence sequence, TimeProvider clock) : IProductCodeService
{
    public Task<string> NewConsignmentCodeAsync(CancellationToken ct) =>
        NextAsync("CD", clock.GetUtcNow().ToString("ddMM", CultureInfo.InvariantCulture), ct);
    public Task<string> NewProductSkuAsync(CancellationToken ct) =>
        NextAsync("CC", clock.GetUtcNow().ToString("yyyy", CultureInfo.InvariantCulture), ct);

    private async Task<string> NextAsync(string prefix, string period, CancellationToken ct)
    {
        var value = await sequence.NextAsync(ct);
        if (value is < 1 or > 999_999)
            throw new InvalidOperationException("Đã hết dải mã sản phẩm. Vui lòng liên hệ quản trị viên.");
        return prefix == "CD" ? $"{prefix}-{value:D2}-{period}" : $"{prefix}-{period}-{value:D6}";
    }
}
