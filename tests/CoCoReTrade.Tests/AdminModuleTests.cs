using System.Collections.Concurrent;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using CoCoReTrade.Infrastructure;
using CoCoReTrade.Web.Models;
using CoCoReTrade.Web.Controllers.Api.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Tests;

public sealed class AdminModuleTests
{
    private static Product NewProduct(string code) => Product.Submit("seller", code, "Linen shirt", "Zara", "Shirts", "M",
        500_000, 300_000, 95, true,
        [new ProductImage("a.jpg", "image/jpeg", 0), new ProductImage("b.jpg", "image/jpeg", 1), new ProductImage("c.jpg", "image/jpeg", 2)]);
    private static InspectionSubmission Submission(string version, bool passed = true) =>
        new(passed, "Linen shirt", "Zara", "Shirts", "M", 500_000, 280_000, 92,
            "Physical item checked", version, []);
    private static (AdminProductService Service, MemoryAdminStore Store) Setup(params Product[] products)
    {
        var store = new MemoryAdminStore(products);
        var permissions = new MemoryPermissions();
        var codes = new ProductCodeService(store, TimeProvider.System);
        return (new AdminProductService(store, permissions, new MemoryImages(), codes,
            new ProductLifecycleService(permissions), TimeProvider.System), store);
    }

    [Fact]
    public async Task UnauthorizedActorsCannotInspectPublishOrReject()
    {
        var product = NewProduct("CG-2026-000001");
        var (service, store) = Setup(product);
        var version = Convert.ToBase64String(product.RowVersion);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PendingAsync("buyer", null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.InspectAsync("buyer", product.Id, Submission(version), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PublishAsync("inspector", product.Id, version, "", true, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RejectAsync("inspector", product.Id, "Damaged item", version, default));
        Assert.Equal(ProductStatus.PendingApproval, product.Status);
        Assert.Empty(store.Audits);
    }

    [Fact]
    public void DomainRequiresPhysicalInspectionAndDisallowsSellerSelfInspection()
    {
        var product = NewProduct("CG-2026-000009");
        Assert.Throws<InvalidOperationException>(() => product.Publish(DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => product.Reject("Not acceptable"));
        Assert.Throws<UnauthorizedAccessException>(() => product.RecordInspection("seller", true,
            product.Name, product.Brand, product.Category, product.Size, product.OriginalPrice,
            product.AskingPrice, 95, "Self inspection", "CC-2026-000009", [], DateTime.UtcNow));
    }

    [Fact]
    public async Task InspectorCannotOpenNonPendingManagementDetails()
    {
        var product = NewProduct("CG-2026-000011");
        product.RecordInspection("inspector", true, product.Name, product.Brand, product.Category,
            product.Size, product.OriginalPrice, product.AskingPrice, 95, "Physical item checked",
            "CC-2026-000011", [], DateTime.UtcNow);
        product.Publish(DateTime.UtcNow);
        var (service, _) = Setup(product);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync("inspector", product.Id, default));
        Assert.NotNull(await service.GetAsync("admin", product.Id, default));
    }

    [Fact]
    public async Task InspectorRecordsVerifiedDetailsAndAdminPublishesSeparately()
    {
        var product = NewProduct("CG-2026-000002");
        var (service, store) = Setup(product);
        var inspected = await service.InspectAsync("inspector", product.Id,
            Submission(Convert.ToBase64String(product.RowVersion)), default);
        Assert.Equal(ProductStatus.PendingApproval, product.Status);
        Assert.Equal(92, product.VerifiedCondition);
        Assert.Matches(@"^CC-\d{4}-\d{6}$", product.Sku);
        Assert.True(inspected.CanPublish);
        Assert.Single(store.TrackedInspections);
        Assert.Single(store.Audits);
        Assert.Empty(store.Notifications);

        await Assert.ThrowsAsync<ProductConcurrencyException>(() => service.PublishAsync("admin", product.Id,
            Convert.ToBase64String(new byte[] { 1 }), product.Sku, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.PublishAsync("admin", product.Id,
            inspected.RowVersion, "CC-2026-999999", true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.PublishAsync("admin", product.Id,
            inspected.RowVersion, product.Sku, false, default));
        var published = await service.PublishAsync("admin", product.Id, inspected.RowVersion, product.Sku, true, default);
        Assert.Equal(ProductStatus.Active, published.Product.Status);
        Assert.Equal(2, store.Audits.Count);
        Assert.Single(store.Notifications);
        Assert.Equal("seller", store.Notifications.Single().UserId);
    }

    [Fact]
    public async Task FailedInspectionCannotPublishAndRejectionRequiresReasonAndNotifiesSeller()
    {
        var product = NewProduct("CG-2026-000003");
        var (service, store) = Setup(product);
        var inspected = await service.InspectAsync("inspector", product.Id,
            Submission(Convert.ToBase64String(product.RowVersion), false), default);
        Assert.False(inspected.CanPublish);
        Assert.Empty(product.Sku);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync("admin", product.Id,
            inspected.RowVersion, "CC-2026-000001", true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RejectAsync("admin", product.Id, "bad", inspected.RowVersion, default));
        var rejected = await service.RejectAsync("admin", product.Id, "Fabric has extensive damage", inspected.RowVersion, default);
        Assert.Equal(ProductStatus.Rejected, rejected.Product.Status);
        Assert.Contains("damage", store.Notifications.Single().Message);
        Assert.Equal(2, store.Audits.Count);
    }

    [Fact]
    public async Task InspectionPhotosAreLinkedToTheInspectionAndProduct()
    {
        var product = NewProduct("CG-2026-000010");
        var (service, _) = Setup(product);
        var image = new ImageUpload("studio.jpg", "image/jpeg", 100, () => new MemoryStream(new byte[100]));
        var request = Submission(Convert.ToBase64String(product.RowVersion)) with { Images = [image] };
        var result = await service.InspectAsync("inspector", product.Id, request, default);
        var inspection = Assert.Single(product.Inspections);
        var photo = Assert.Single(inspection.Images);
        Assert.Equal(inspection.Id, photo.InspectionId);
        Assert.Contains(photo.Id, result.Product.ImageIds);
        Assert.Contains(photo.Id, result.Inspections.Single().ImageIds);
    }

    [Fact]
    public async Task InventorySkusAreUniqueAndSequenceHandlesParallelAllocations()
    {
        var first = NewProduct("CG-2026-000004");
        var second = NewProduct("CG-2026-000005");
        var (service, store) = Setup(first, second);
        await service.InspectAsync("inspector", first.Id, Submission(Convert.ToBase64String(first.RowVersion)), default);
        await service.InspectAsync("inspector", second.Id, Submission(Convert.ToBase64String(second.RowVersion)), default);
        Assert.NotEqual(first.Sku, second.Sku);
        var allocations = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => store.NextAsync(default)));
        Assert.Equal(allocations.Length, allocations.Distinct().Count());
    }

    [Fact]
    public void SkuAndConsignmentCodeHaveUniqueDatabaseIndexes()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=MetadataOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var indexes = db.Model.FindEntityType(typeof(Product))!.GetIndexes().ToList();
        Assert.Contains(indexes, x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(Product.Sku)));
        Assert.Contains(indexes, x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(Product.ConsignmentCode)));
    }

    [Fact]
    public void PublishEndpointRequiresExplicitPolicyAndQrRendersLocally()
    {
        var method = typeof(AdminInspectionController).GetMethod(nameof(AdminInspectionController.Publish))!;
        Assert.Contains(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>(),
            x => x.Policy == StaffPermissions.Publish);
        Assert.Contains("<svg", SkuQrCode.Svg("CC-2026-000001"));
        Assert.Throws<ArgumentException>(() => SkuQrCode.Svg("../pending"));
    }

    private sealed class MemoryPermissions : IStaffPermissionChecker
    {
        public Task EnsureAsync(string actorId, string permission, CancellationToken ct)
        {
            var allowed = actorId switch
            {
                "inspector" => permission is StaffPermissions.ReadPending or StaffPermissions.Inspect,
                "admin" => true,
                _ => false
            };
            return allowed ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
        }
    }
    private sealed class MemoryImages : IPrivateImageStore
    {
        public Task<(string Key, string ContentType)> SaveAsync(ImageUpload image, CancellationToken ct) =>
            Task.FromResult((Guid.NewGuid().ToString("N") + ".jpg", "image/jpeg"));
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
        public Task<Stream> OpenAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
    }
    private sealed class MemoryTransaction : IProductTransaction
    {
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class MemoryAdminStore : IAdminProductRepository, IProductCodeSequence
    {
        private readonly ConcurrentDictionary<Guid, Product> products;
        private long sequence;
        private long rowVersion = 1;
        public List<AuditLog> Audits { get; } = [];
        public List<Inspection> TrackedInspections { get; } = [];
        public List<MarketplaceNotification> Notifications { get; } = [];
        public MemoryAdminStore(Product[] initial)
        {
            products = new(initial.ToDictionary(x => x.Id));
            foreach (var p in initial) SetVersion(p, BitConverter.GetBytes(Interlocked.Increment(ref rowVersion)));
        }
        public Task<Product?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(products.GetValueOrDefault(id));
        public Task<IReadOnlyList<AdminProductItem>> ListAsync(ProductStatus? status, string? search, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AdminProductItem>>(products.Values.Where(x => status is null || x.Status == status)
                .Select(x => new AdminProductItem(x.Id, x.ConsignmentCode, x.Sku, x.Name, x.Brand, "Seller", x.Status,
                    x.VerifiedCondition, x.CreatedAtUtc)).ToList());
        public Task<AdminProductDashboard> DashboardAsync(CancellationToken ct) =>
            Task.FromResult(new AdminProductDashboard(products.Values.Count(x => x.Status == ProductStatus.PendingApproval),
                0, products.Values.Count(x => x.Status == ProductStatus.Active), 0, 0, 0));
        public Task<IReadOnlyList<AuditEntry>> AuditAsync(Guid? productId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AuditEntry>>(Audits.Select(x => new AuditEntry(x.Id, x.ActorId, x.Action,
                x.EntityType, x.EntityId, x.Details, x.CreatedAtUtc)).ToList());
        public Task<string> SellerNameAsync(string sellerId, CancellationToken ct) => Task.FromResult("Seller");
        public Task<long> NextAsync(CancellationToken ct) => Task.FromResult(Interlocked.Increment(ref sequence));
        public Task<IProductTransaction> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IProductTransaction>(new MemoryTransaction());
        public void AddInspection(Inspection inspection) => TrackedInspections.Add(inspection);
        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(MarketplaceNotification notification) => Notifications.Add(notification);
        public Task SaveAsync(CancellationToken ct)
        {
            foreach (var p in products.Values) SetVersion(p, BitConverter.GetBytes(Interlocked.Increment(ref rowVersion)));
            return Task.CompletedTask;
        }
        private static void SetVersion(Product p, byte[] version) => typeof(Product).GetProperty(nameof(Product.RowVersion))!.SetValue(p, version);
    }
}
