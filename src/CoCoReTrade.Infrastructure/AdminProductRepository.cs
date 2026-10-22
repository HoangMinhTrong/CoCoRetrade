using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoCoReTrade.Infrastructure;

public sealed class AdminProductRepository(AppDbContext db) : IAdminProductRepository
{
    public Task<Product?> FindAsync(Guid id, CancellationToken ct) => db.Products
        .Include(x => x.Images).Include(x => x.Inspections).ThenInclude(x => x.Images)
        .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<AdminProductItem>> ListAsync(ProductStatus? status, string? search, CancellationToken ct)
    {
        var query = from p in db.Products.AsNoTracking()
                    join u in db.Users.AsNoTracking() on p.SellerId equals u.Id
                    select new { Product = p, SellerName = u.FullName };
        if (status.HasValue) query = query.Where(x => x.Product.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var compactCode = term.Replace("-", "").Replace(" ", "").ToUpperInvariant();
            query = query.Where(x => x.Product.ConsignmentCode.Replace("-", "").Replace(" ", "").ToUpper().Contains(compactCode) ||
                x.Product.Sku.Replace("-", "").Replace(" ", "").ToUpper().Contains(compactCode) ||
                x.Product.Name.Contains(term) || x.SellerName.Contains(term));
        }
        return await query.OrderByDescending(x => x.Product.CreatedAtUtc).Take(200)
            .Select(x => new AdminProductItem(x.Product.Id, x.Product.ConsignmentCode, x.Product.Sku,
                x.Product.Name, x.Product.Brand, x.SellerName, x.Product.Status,
                x.Product.VerifiedCondition, x.Product.CreatedAtUtc, x.Product.PreliminaryStatus)).ToListAsync(ct);
    }
    public async Task<AdminProductDashboard> DashboardAsync(CancellationToken ct)
    {
        var groups = await db.Products.AsNoTracking().GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        int Count(ProductStatus status) => groups.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var inspected = await db.Products.AsNoTracking().CountAsync(x => x.Status == ProductStatus.PendingApproval && x.LatestInspectionId != null, ct);
        return new AdminProductDashboard(Count(ProductStatus.PendingApproval), inspected, Count(ProductStatus.Active),
            Count(ProductStatus.Rejected), Count(ProductStatus.Reserved), Count(ProductStatus.Sold));
    }
    public async Task<IReadOnlyList<AuditEntry>> AuditAsync(Guid? productId, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (productId.HasValue)
        {
            var id = productId.Value.ToString();
            query = query.Where(x => x.EntityType == "Product" && x.EntityId == id);
        }
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(200)
            .Select(x => new AuditEntry(x.Id, x.ActorId, x.Action, x.EntityType, x.EntityId,
                x.Details, x.CreatedAtUtc)).ToListAsync(ct);
    }
    public async Task<string> SellerNameAsync(string sellerId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => x.Id == sellerId).Select(x => x.FullName).FirstOrDefaultAsync(ct) ?? sellerId;
    public async Task<IProductTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new ProductTransaction(await db.Database.BeginTransactionAsync(ct));
    public void AddInspection(Inspection inspection) => db.Inspections.Add(inspection);
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed class ProductTransaction(IDbContextTransaction transaction) : IProductTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
