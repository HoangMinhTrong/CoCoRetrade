using System.Text;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;

namespace CoCoReTrade.Web.Preview;

// Development-only catalogue. It is registered only when Preview:Enabled is true.
public sealed class PreviewProductService : IProductService
{
    private sealed record Sample(ProductCard Card, decimal OriginalPrice, string Color, string Shape);
    private static readonly Sample[] Samples =
    [
        Create(1, "Áo khoác denim vintage", "Zara", "Áo", "M", 320_000, 650_000, 95, "#8097aa", "coat"),
        Create(2, "Đầm midi hoa nhí", "Mango", "Đầm/Váy", "S", 420_000, 890_000, 99, "#ae818c", "dress"),
        Create(3, "Quần ống rộng linen", "Uniqlo", "Quần", "M", 260_000, 590_000, 95, "#c4ad91", "pants"),
        Create(4, "Túi đeo vai cổ điển", "Charles & Keith", "Túi", "Free size", 540_000, 1_100_000, 90, "#79574d", "bag"),
        Create(5, "Áo sơ mi cotton", "COS", "Áo", "L", 380_000, 790_000, 99, "#dfc8a7", "shirt"),
        Create(6, "Blazer dáng suông", "Massimo Dutti", "Áo", "S", 750_000, 1_700_000, 95, "#706c70", "coat")
    ];

    private static Sample Create(int number, string name, string brand, string category, string size,
        decimal askingPrice, decimal originalPrice, int condition, string color, string shape)
    {
        var id = Guid.Parse($"00000000-0000-0000-0000-{number:D12}");
        var imageId = Guid.Parse($"11111111-1111-1111-1111-{number:D12}");
        return new Sample(new ProductCard(id, $"CC-2026-{number:D6}", name, brand, category, size,
            askingPrice, condition, ProductStatus.Active, imageId, "preview-seller"), originalPrice, color, shape);
    }

    public Task<IReadOnlyList<ProductCard>> ShopAsync(ProductFilter filter, CancellationToken ct)
    {
        IEnumerable<ProductCard> cards = Samples.Select(x => x.Card);
        if (!string.IsNullOrWhiteSpace(filter.Search))
            cards = cards.Where(x => new[] { x.Name, x.Brand, x.Sku }.Any(y => y.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(filter.Category)) cards = cards.Where(x => x.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Brand)) cards = cards.Where(x => x.Brand.Contains(filter.Brand, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Size)) cards = cards.Where(x => x.Size.Equals(filter.Size, StringComparison.OrdinalIgnoreCase));
        if (filter.MinPrice.HasValue) cards = cards.Where(x => x.AskingPrice >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue) cards = cards.Where(x => x.AskingPrice <= filter.MaxPrice.Value);
        if (filter.MinCondition.HasValue) cards = cards.Where(x => x.ConditionPercent >= filter.MinCondition.Value);
        cards = filter.Sort switch
        {
            "price_asc" => cards.OrderBy(x => x.AskingPrice),
            "price_desc" => cards.OrderByDescending(x => x.AskingPrice),
            _ => cards.OrderByDescending(x => x.Sku)
        };
        return Task.FromResult<IReadOnlyList<ProductCard>>(cards.ToList());
    }

    public Task<ProductDetails?> VisibleDetailsAsync(Guid id, string? viewerId, bool isAdmin, CancellationToken ct)
    {
        var sample = Samples.FirstOrDefault(x => x.Card.Id == id);
        ProductDetails? details = sample is null ? null : new ProductDetails(sample.Card.Id, sample.Card.Sku,
            sample.Card.Name, sample.Card.Brand, sample.Card.Category, sample.Card.Size, sample.OriginalPrice,
            sample.Card.AskingPrice, sample.Card.ConditionPercent, sample.Card.ConditionPercent, true,
            ProductStatus.Active, sample.Card.SellerId, null, [sample.Card.CoverImageId!.Value], DateTime.UtcNow);
        return Task.FromResult(details);
    }

    public Task<(Stream Stream, string ContentType)?> VisibleImageAsync(Guid imageId, string? viewerId,
        bool canInspectPending, bool canManageAll, CancellationToken ct)
    {
        var sample = Samples.FirstOrDefault(x => x.Card.CoverImageId == imageId);
        if (sample is null) return Task.FromResult<(Stream, string)?>(null);
        var shape = sample.Shape switch
        {
            "dress" => "<path d='M265 180h110l48 118-48 30 75 260H190l75-260-48-30z'/><path d='M265 180q55 85 110 0' fill='none' stroke='#fff' stroke-opacity='.55' stroke-width='9'/>",
            "pants" => "<path d='M222 175h196l-21 408h-77l-18-265-19 265h-76z'/><path d='M222 228h196M320 178v90' fill='none' stroke='#fff' stroke-opacity='.35' stroke-width='8'/>",
            "bag" => "<rect x='172' y='276' width='296' height='240' rx='38'/><path d='M242 279v-62a78 78 0 0 1 156 0v62' fill='none' stroke='#664c42' stroke-width='24'/><path d='M200 365h240' fill='none' stroke='#fff' stroke-opacity='.35' stroke-width='7'/>",
            "shirt" => "<path d='M258 178 201 203l-68 95 69 53 38-44v255h160V307l38 44 69-53-68-95-57-25-62 65z'/><path d='M259 178 320 244l61-66M320 244v318' fill='none' stroke='#fff' stroke-opacity='.5' stroke-width='8'/>",
            _ => "<path d='M250 169 195 195l-82 101 73 58 48-54 12 276h148l12-276 48 54 73-58-82-101-55-26-70 65z'/><path d='M250 169 320 238l70-69M320 238v338M233 348h45m84 0h45' fill='none' stroke='#fff' stroke-opacity='.48' stroke-width='8'/>"
        };
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="640" height="760" viewBox="0 0 640 760">
              <rect width="640" height="760" fill="#eee6df"/>
              <circle cx="470" cy="140" r="210" fill="#dfcec5" opacity=".55"/>
              <circle cx="130" cy="650" r="210" fill="#faf3eb" opacity=".65"/>
              <ellipse cx="320" cy="635" rx="182" ry="25" fill="#8f8178" opacity=".2"/>
              <g fill="{sample.Color}" stroke="#675753" stroke-opacity=".24" stroke-width="5" stroke-linejoin="round">{shape}</g>
              <text x="320" y="700" text-anchor="middle" font-family="Georgia,serif" font-size="28" letter-spacing="8" fill="#866e68">COCO RETRADE</text>
            </svg>
            """;
        return Task.FromResult<(Stream, string)?>(
            (new MemoryStream(Encoding.UTF8.GetBytes(svg), writable: false), "image/svg+xml"));
    }

    public Task<ProductDetails> SubmitAsync(string sellerId, SubmitListing request, CancellationToken ct) =>
        throw new NotSupportedException("Preview mode is read-only.");
    public Task<ProductDetails> EditAsync(string sellerId, Guid id, EditListing request, CancellationToken ct) =>
        throw new NotSupportedException("Preview mode is read-only.");
    public Task<IReadOnlyList<ProductCard>> ClosetAsync(string sellerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProductCard>>([]);
    public Task<IReadOnlyList<ProductCard>> PendingAsync(string? search, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProductCard>>([]);
}
