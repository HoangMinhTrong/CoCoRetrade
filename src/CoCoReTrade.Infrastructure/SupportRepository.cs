using System.Data;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class SupportRepository(AppDbContext db) : ISupportRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await work();
        await transaction.CommitAsync(ct);
        return result;
    }
    public Task<SupportTicket?> FindAsync(Guid id, CancellationToken ct) =>
        db.SupportTickets.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<SupportTicketRow>> ListAsync(string? ownerId, CancellationToken ct)
    {
        var q = from ticket in db.SupportTickets.AsNoTracking()
                join user in db.Users.AsNoTracking() on ticket.UserId equals user.Id
                where ownerId == null || ticket.UserId == ownerId
                select new SupportTicketRow(ticket.Id, ticket.Subject, ticket.Status,
                    ticket.UserId, user.FullName, ticket.LastActivityAtUtc);
        return await q.OrderByDescending(x => x.LastActivityAtUtc).Take(100).ToListAsync(ct);
    }
    public Task<SupportTicketRow?> RowAsync(Guid id, CancellationToken ct) =>
        (from ticket in db.SupportTickets.AsNoTracking()
         join user in db.Users.AsNoTracking() on ticket.UserId equals user.Id
         where ticket.Id == id
         select new SupportTicketRow(ticket.Id, ticket.Subject, ticket.Status,
             ticket.UserId, user.FullName, ticket.LastActivityAtUtc)).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<string>> AdminIdsAsync(CancellationToken ct) =>
        await db.UserRoles.Join(db.Roles, x => x.RoleId, x => x.Id,
                (userRole, role) => new { userRole.UserId, role.Name })
            .Where(x => x.Name == "Admin").Select(x => x.UserId).Distinct().ToListAsync(ct);
    public void Add(SupportTicket ticket) => db.SupportTickets.Add(ticket);
    public void AddNotification(MarketplaceNotification notification) => db.MarketplaceNotifications.Add(notification);
    public void AddAudit(AuditLog audit) => db.AuditLogs.Add(audit);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
