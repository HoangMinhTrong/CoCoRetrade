using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record ImageUpload(string FileName, string ContentType, long Length, Func<Stream> OpenReadStream);
public sealed record SubmitListing(string Name, string Brand, string Category, string Size, decimal OriginalPrice,
    decimal AskingPrice, int ConditionPercent, bool AllowOffers, IReadOnlyCollection<ImageUpload> Images);
public sealed record ProductFilter(string? Search = null, string? Category = null, string? Brand = null, string? Size = null,
    decimal? MinPrice = null, decimal? MaxPrice = null, int? MinCondition = null, string Sort = "newest",
    string? Fulfillment = null, string? SellerId = null, IReadOnlyCollection<Guid>? Ids = null);
public sealed record ProductCard(Guid Id, string Sku, string Name, string Brand, string Category, string Size,
    decimal AskingPrice, int ConditionPercent, ProductStatus Status, Guid? CoverImageId, string SellerId,
    string ConsignmentCode = "", string SellerName = "", int LikeCount = 0, bool SellerHasAvatar = false);
public sealed record ProductCardMetadata(string SellerName, int LikeCount, bool SellerHasAvatar);

public interface IProductCardMetadataRepository
{
    Task<IReadOnlyDictionary<Guid, ProductCardMetadata>> GetAsync(IReadOnlyCollection<Product> products, CancellationToken ct);
}
public sealed record ProductDetails(Guid Id, string Sku, string Name, string Brand, string Category, string Size,
    decimal OriginalPrice, decimal AskingPrice, int DeclaredCondition, int? VerifiedCondition, bool AllowOffers,
    ProductStatus Status, string SellerId, string? RejectionReason, IReadOnlyList<Guid> ImageIds, DateTime CreatedAtUtc,
    string ConsignmentCode = "");

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task<Product?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<Product?> FindByImageAsync(Guid imageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> ActiveAsync(ProductFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> OwnedAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> PendingAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> InspectorUserIdsAsync(CancellationToken cancellationToken);
    void AddNotification(MarketplaceNotification notification);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IPrivateImageStore
{
    Task<(string Key, string ContentType)> SaveAsync(ImageUpload image, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
    Task<Stream> OpenAsync(string key, CancellationToken cancellationToken);
}

public interface IProductService
{
    Task<ProductDetails> SubmitAsync(string sellerId, SubmitListing request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductCard>> ShopAsync(ProductFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductCard>> ClosetAsync(string sellerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductCard>> PendingAsync(string? search, CancellationToken cancellationToken);
    Task<ProductDetails?> VisibleDetailsAsync(Guid id, string? viewerId, bool isAdmin, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType)?> VisibleImageAsync(Guid imageId, string? viewerId,
        bool canInspectPending, bool canManageAll, CancellationToken cancellationToken);
}

public sealed class ProductService(IProductRepository repository, IPrivateImageStore images,
    IProductCodeService codes, IProductCardMetadataRepository? metadata = null) : IProductService
{
    public async Task<ProductDetails> SubmitAsync(string sellerId, SubmitListing request, CancellationToken ct)
    {
        if (request.Images.Count is < 3 or > 10) throw new ArgumentException("Tải từ 3 đến 10 ảnh sản phẩm.");
        var keys = new List<string>();
        try
        {
            var productImages = new List<ProductImage>();
            foreach (var image in request.Images)
            {
                var (key, type) = await images.SaveAsync(image, ct);
                keys.Add(key);
                productImages.Add(new ProductImage(key, type, productImages.Count));
            }
            var code = await codes.NewConsignmentCodeAsync(ct);
            var product = Product.Submit(sellerId, code, request.Name, request.Brand, request.Category, request.Size,
                request.OriginalPrice, request.AskingPrice, request.ConditionPercent, request.AllowOffers, productImages);
            await repository.AddAsync(product, ct);
            foreach (var inspectorId in await repository.InspectorUserIdsAsync(ct))
                if (inspectorId != sellerId) repository.AddNotification(new MarketplaceNotification
                {
                    Id = Guid.NewGuid(), UserId = inspectorId, Category = "Listings",
                    Message = $"Ký gửi mới {code} đang chờ kiểm định.", CreatedAtUtc = DateTime.UtcNow
                });
            await repository.SaveAsync(ct);
            return ToDetails(product);
        }
        catch
        {
            foreach (var key in keys) await images.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<ProductCard>> ShopAsync(ProductFilter filter, CancellationToken ct) =>
        await CardsAsync(await repository.ActiveAsync(filter, ct), ct);
    public async Task<IReadOnlyList<ProductCard>> ClosetAsync(string sellerId, CancellationToken ct) =>
        await CardsAsync(await repository.OwnedAsync(sellerId, ct), ct);
    public async Task<IReadOnlyList<ProductCard>> PendingAsync(string? search, CancellationToken ct) =>
        await CardsAsync(await repository.PendingAsync(search, ct), ct);
    private async Task<IReadOnlyList<ProductCard>> CardsAsync(IReadOnlyList<Product> products, CancellationToken ct)
    {
        if (metadata is null) return products.Select(ToCard).ToList();
        var values = await metadata.GetAsync(products, ct);
        return products.Select(p => values.TryGetValue(p.Id, out var value)
            ? ToCard(p) with { SellerName = value.SellerName, LikeCount = value.LikeCount,
                SellerHasAvatar = value.SellerHasAvatar }
            : ToCard(p)).ToList();
    }
    public async Task<ProductDetails?> VisibleDetailsAsync(Guid id, string? viewerId, bool isAdmin, CancellationToken ct)
    {
        var product = await repository.FindAsync(id, ct);
        return product is not null && CanView(product, viewerId, isAdmin) ? ToDetails(product) : null;
    }
    public async Task<(Stream Stream, string ContentType)?> VisibleImageAsync(Guid imageId, string? viewerId,
        bool canInspectPending, bool canManageAll, CancellationToken ct)
    {
        // Repository lookup is deliberately by image owner/product, never by a caller-provided storage path.
        var product = await repository.FindByImageAsync(imageId, ct);
        var image = product?.Images.FirstOrDefault(x => x.Id == imageId);
        if (product is null || image is null ||
            !(CanView(product, viewerId, canManageAll) || canInspectPending && product.Status == ProductStatus.PendingApproval)) return null;
        return (await images.OpenAsync(image.StorageKey, ct), image.ContentType);
    }
    private static bool CanView(Product p, string? viewerId, bool isAdmin) =>
        p.Status == ProductStatus.Active || isAdmin || (viewerId is not null && p.SellerId == viewerId);
    private static ProductCard ToCard(Product p) => new(p.Id, p.Sku, p.Name, p.Brand, p.Category, p.Size,
        p.AskingPrice, p.VerifiedCondition ?? p.DeclaredCondition, p.Status, p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Id, p.SellerId,
        p.ConsignmentCode);
    private static ProductDetails ToDetails(Product p) => new(p.Id, p.Sku, p.Name, p.Brand, p.Category, p.Size,
        p.OriginalPrice, p.AskingPrice, p.DeclaredCondition, p.VerifiedCondition, p.AllowOffers, p.Status, p.SellerId,
        p.RejectionReason, p.Images.OrderBy(i => i.SortOrder).Select(i => i.Id).ToList(), p.CreatedAtUtc, p.ConsignmentCode);
}
