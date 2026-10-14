using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AddressRepository(AppDbContext db) : IAddressRepository
{
    public async Task<IReadOnlyList<Address>> ForUserAsync(string userId, CancellationToken ct) =>
        await db.Addresses.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.RecipientName).Take(100).ToListAsync(ct);
    public Task<Address?> OwnedAsync(Guid id, string userId, CancellationToken ct) =>
        db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    public void Add(Address address) => db.Addresses.Add(address);
    public void Remove(Address address) => db.Addresses.Remove(address);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
