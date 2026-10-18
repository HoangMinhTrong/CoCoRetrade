using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public static class StaffPermissions
{
    public const string ReadPending = "Products.ReadPending";
    public const string Inspect = "Products.Inspect";
    public const string Publish = "Products.Publish";
    public const string Reject = "Products.Reject";
    public const string ManageProducts = "Products.Manage";
    public const string ManageOrders = "Orders.Manage";
    public const string ManageUsers = "Users.Manage";
    public const string ManageWallets = "Wallets.Manage";
    public const string ReadAudit = "Audit.Read";
    public const string ClaimType = "permission";
    public static string[] RequiredRoles(string permission) => permission switch
    {
        ReadPending or Inspect or Publish => ["Inspector", "Admin"],
        Reject or ManageProducts or ManageOrders or ManageUsers or ManageWallets or ReadAudit => ["Admin"],
        _ => []
    };
}

public sealed class ProductConcurrencyException(string message) : InvalidOperationException(message);

public sealed record InspectionSubmission(bool Passed, string Name, string Brand, string Category, string Size,
    decimal OriginalPrice, decimal AskingPrice, int VerifiedCondition, string? Notes, string ExpectedRowVersion,
    IReadOnlyCollection<ImageUpload> Images);
public sealed record InspectionSummary(Guid Id, string InspectorId, bool Passed, string Name, string Brand,
    string Category, string Size, decimal OriginalPrice, decimal AskingPrice, int VerifiedCondition,
    string Notes, DateTime CreatedAtUtc, IReadOnlyList<Guid> ImageIds);
public sealed record AdminProductDetails(ProductDetails Product, string SellerName, string RowVersion,
    IReadOnlyList<InspectionSummary> Inspections, bool CanPublish);
public sealed record AdminProductItem(Guid Id, string ConsignmentCode, string Sku, string Name, string Brand,
    string SellerName, ProductStatus Status, int? VerifiedCondition, DateTime CreatedAtUtc);
public sealed record AdminProductDashboard(int Pending, int Inspected, int Active, int Rejected, int Reserved, int Sold);
public sealed record AuditEntry(Guid Id, string ActorId, string Action, string EntityType, string EntityId,
    string Details, DateTime CreatedAtUtc);

public interface IStaffPermissionChecker
{
    Task EnsureAsync(string actorId, string permission, CancellationToken ct);
}

public interface IProductTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}

public interface IAdminProductRepository
{
    Task<Product?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdminProductItem>> ListAsync(ProductStatus? status, string? search, CancellationToken ct);
    Task<AdminProductDashboard> DashboardAsync(CancellationToken ct);
    Task<IReadOnlyList<AuditEntry>> AuditAsync(Guid? productId, CancellationToken ct);
    Task<string> SellerNameAsync(string sellerId, CancellationToken ct);
    Task<IProductTransaction> BeginTransactionAsync(CancellationToken ct);
    void AddInspection(Inspection inspection);
    void AddAudit(AuditLog audit);
    void AddNotification(MarketplaceNotification notification);
    Task SaveAsync(CancellationToken ct);
}

public interface IAdminProductService
{
    Task<AdminProductDashboard> DashboardAsync(string actorId, CancellationToken ct);
    Task<IReadOnlyList<AdminProductItem>> PendingAsync(string actorId, string? search, CancellationToken ct);
    Task<IReadOnlyList<AdminProductItem>> ManageAsync(string actorId, ProductStatus? status, string? search, CancellationToken ct);
    Task<AdminProductDetails?> GetAsync(string actorId, Guid id, CancellationToken ct);
    Task<AdminProductDetails> InspectAsync(string actorId, Guid id, InspectionSubmission request, CancellationToken ct);
    Task<AdminProductDetails> SelectCoverAsync(string actorId, Guid id, Guid imageId,
        string expectedRowVersion, CancellationToken ct);
    Task<AdminProductDetails> PublishAsync(string actorId, Guid id, string expectedRowVersion,
        bool physicalItemConfirmed, CancellationToken ct);
    Task<AdminProductDetails> RejectAsync(string actorId, Guid id, string reason, string expectedRowVersion, CancellationToken ct);
    Task<IReadOnlyList<AuditEntry>> AuditAsync(string actorId, Guid? productId, CancellationToken ct);
}

public sealed class AdminProductService(IAdminProductRepository repository, IStaffPermissionChecker permissions,
    IPrivateImageStore images, IProductCodeService codes, IProductLifecycleService lifecycle,
    TimeProvider clock) : IAdminProductService
{
    public async Task<AdminProductDashboard> DashboardAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ReadPending, ct);
        return await repository.DashboardAsync(ct);
    }
    public async Task<IReadOnlyList<AdminProductItem>> PendingAsync(string actorId, string? search, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ReadPending, ct);
        return await repository.ListAsync(ProductStatus.PendingApproval, search, ct);
    }
    public async Task<IReadOnlyList<AdminProductItem>> ManageAsync(string actorId, ProductStatus? status, string? search, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageProducts, ct);
        return await repository.ListAsync(status, search, ct);
    }
    public async Task<AdminProductDetails?> GetAsync(string actorId, Guid id, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ReadPending, ct);
        var product = await repository.FindAsync(id, ct);
        if (product is not null && product.Status != ProductStatus.PendingApproval)
            await permissions.EnsureAsync(actorId, StaffPermissions.ManageProducts, ct);
        return product is null ? null : await ToDetailsAsync(product, ct);
    }
    public async Task<AdminProductDetails> InspectAsync(string actorId, Guid id, InspectionSubmission request, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Inspect, ct);
        if (request is null || request.Images is null) throw new ArgumentException("Cần nhập kết quả kiểm định.");
        if (request.Images.Count > 10) throw new ArgumentException("Chỉ được tải tối đa 10 ảnh kiểm định.");
        var keys = new List<string>();
        var committed = false;
        try
        {
            var uploaded = new List<ProductImage>();
            foreach (var image in request.Images)
            {
                var (key, type) = await images.SaveAsync(image, ct);
                keys.Add(key);
                uploaded.Add(new ProductImage(key, type, uploaded.Count));
            }
            await using var transaction = await repository.BeginTransactionAsync(ct);
            var product = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            EnsureVersion(product, request.ExpectedRowVersion);
            var inspection = await lifecycle.InspectAsync(actorId, product, request,
                uploaded, clock.GetUtcNow().UtcDateTime, ct);
            repository.AddInspection(inspection);
            repository.AddAudit(NewAudit(actorId, "Products.Inspected", product,
                $"{(request.Passed ? "Passed" : "Failed")}; condition={request.VerifiedCondition}%; SKU={product.Sku}; notes={request.Notes?.Trim() ?? string.Empty}"));
            await repository.SaveAsync(ct);
            var result = await ToDetailsAsync(product, ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return result;
        }
        catch
        {
            if (!committed) foreach (var key in keys) await images.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }
    public async Task<AdminProductDetails> SelectCoverAsync(string actorId, Guid id, Guid imageId,
        string expectedRowVersion, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Inspect, ct);
        await using var transaction = await repository.BeginTransactionAsync(ct);
        var product = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
        EnsureVersion(product, expectedRowVersion);
        product.SelectCover(imageId, actorId, clock.GetUtcNow().UtcDateTime);
        repository.AddAudit(NewAudit(actorId, "Products.CoverSelected", product, imageId.ToString()));
        await repository.SaveAsync(ct);
        var result = await ToDetailsAsync(product, ct);
        await transaction.CommitAsync(ct);
        return result;
    }
    public async Task<AdminProductDetails> PublishAsync(string actorId, Guid id, string expectedRowVersion,
        bool physicalItemConfirmed, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Publish, ct);
        await using var transaction = await repository.BeginTransactionAsync(ct);
        var product = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
        EnsureVersion(product, expectedRowVersion);
        if (!product.CanPublish) throw new InvalidOperationException("Cần kiểm định hiện vật đạt yêu cầu trước khi đăng bán.");
        if (!physicalItemConfirmed) throw new ArgumentException("Hãy xác nhận hiện vật đạt yêu cầu trước khi đăng bán.");
        var sku = await codes.NewProductSkuAsync(ct);
        await lifecycle.PublishAsync(actorId, product, sku, clock.GetUtcNow().UtcDateTime, ct);
        repository.AddAudit(NewAudit(actorId, "Products.Published", product, product.Sku));
        repository.AddNotification(NewNotification(product.SellerId, $"Sản phẩm {product.ConsignmentCode} đã được duyệt lên sàn với mã SKU {product.Sku}.", product.Id));
        await repository.SaveAsync(ct);
        var result = await ToDetailsAsync(product, ct);
        await transaction.CommitAsync(ct);
        return result;
    }
    public async Task<AdminProductDetails> RejectAsync(string actorId, Guid id, string reason, string expectedRowVersion, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Reject, ct);
        await using var transaction = await repository.BeginTransactionAsync(ct);
        var product = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
        EnsureVersion(product, expectedRowVersion);
        await lifecycle.RejectAsync(actorId, product, reason, ct);
        repository.AddAudit(NewAudit(actorId, "Products.Rejected", product, product.RejectionReason!));
        repository.AddNotification(NewNotification(product.SellerId, $"Sản phẩm {product.ConsignmentCode} bị từ chối: {product.RejectionReason}", product.Id));
        await repository.SaveAsync(ct);
        var result = await ToDetailsAsync(product, ct);
        await transaction.CommitAsync(ct);
        return result;
    }
    public async Task<IReadOnlyList<AuditEntry>> AuditAsync(string actorId, Guid? productId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ReadAudit, ct);
        return await repository.AuditAsync(productId, ct);
    }
    private static void EnsureVersion(Product product, string expected)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(expected ?? ""); }
        catch (FormatException) { throw new ArgumentException("Phiên bản sản phẩm không hợp lệ."); }
        if (supplied.Length == 0 || !supplied.SequenceEqual(product.RowVersion))
            throw new ProductConcurrencyException("Sản phẩm đã thay đổi. Hãy tải lại trang kiểm định.");
    }
    private AuditLog NewAudit(string actorId, string action, Product product, string details) => new()
    {
        Id = Guid.NewGuid(), ActorId = actorId, Action = action, EntityType = "Product",
        EntityId = product.Id.ToString(), Details = details, CreatedAtUtc = clock.GetUtcNow().UtcDateTime
    };
    private MarketplaceNotification NewNotification(string userId, string message, Guid? productId = null) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Category = "Listings", Message = message,
        ProductId = productId, CreatedAtUtc = clock.GetUtcNow().UtcDateTime
    };
    private async Task<AdminProductDetails> ToDetailsAsync(Product p, CancellationToken ct) => new(
        new ProductDetails(p.Id, p.Sku, p.Name, p.Brand, p.Category, p.Size, p.OriginalPrice, p.AskingPrice,
            p.DeclaredCondition, p.VerifiedCondition, p.AllowOffers, p.Status, p.SellerId, p.RejectionReason,
            p.Images.OrderBy(x => x.SortOrder).Select(x => x.Id).ToList(), p.CreatedAtUtc, p.ConsignmentCode),
        await repository.SellerNameAsync(p.SellerId, ct), Convert.ToBase64String(p.RowVersion),
        p.Inspections.OrderByDescending(x => x.CreatedAtUtc).Select(x => new InspectionSummary(x.Id, x.InspectorId,
            x.Approved, x.Name, x.Brand, x.Category, x.Size, x.OriginalPrice, x.AskingPrice, x.VerifiedCondition,
            x.Notes, x.CreatedAtUtc, x.Images.Select(i => i.Id).ToList())).ToList(),
        p.CanPublish);
}
