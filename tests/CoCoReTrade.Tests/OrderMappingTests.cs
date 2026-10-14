using CoCoReTrade.Domain;
using CoCoReTrade.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Tests;

public sealed class OrderMappingTests
{
    [Fact]
    public void SqlServerModelProtectsUniqueActiveOrderAndIdempotency()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelInspectionOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var product = db.Model.FindEntityType(typeof(Product))!;
        Assert.True(product.FindProperty(nameof(Product.RowVersion))!.IsConcurrencyToken);
        var order = db.Model.FindEntityType(typeof(Order))!;
        Assert.True(order.FindProperty(nameof(Order.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(order.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(Order.BuyerId), nameof(Order.IdempotencyKey)]));
        Assert.Contains(order.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual([nameof(Order.ProductId)]) && x.GetFilter() == "[Status] <> 'Cancelled'");
    }
}
