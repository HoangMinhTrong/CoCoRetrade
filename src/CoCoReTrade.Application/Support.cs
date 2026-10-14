using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record SupportTicketRow(Guid Id, string Subject, SupportTicketStatus Status,
    string UserId, string UserName, DateTime LastActivityAtUtc);
public sealed record SupportMessageDto(Guid Id, string SenderId, string Body, DateTime CreatedAtUtc);
public sealed record SupportTicketDetails(SupportTicketRow Ticket, IReadOnlyList<SupportMessageDto> Messages);

public interface ISupportRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken);
    Task<SupportTicket?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<string?> OwnerIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicketRow>> ListAsync(string? ownerId, CancellationToken cancellationToken);
    Task<SupportTicketRow?> RowAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> AdminIdsAsync(CancellationToken cancellationToken);
    void Add(SupportTicket ticket);
    void AddMessage(SupportMessage message);
    void AddNotification(MarketplaceNotification notification);
    void AddAudit(AuditLog audit);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface ISupportService
{
    Task<bool> CanAccessAsync(Guid id, string actorId, CancellationToken cancellationToken);
    Task<SupportTicketDetails> CreateAsync(string userId, string subject, string body, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicketRow>> MineAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicketRow>> AdminQueueAsync(string adminId, CancellationToken cancellationToken);
    Task<SupportTicketDetails> DetailsAsync(Guid id, string actorId, CancellationToken cancellationToken);
    Task<SupportTicketDetails> ReplyAsync(Guid id, string actorId, string body, CancellationToken cancellationToken);
    Task<SupportTicketDetails> CloseAsync(Guid id, string adminId, CancellationToken cancellationToken);
}

public interface ISupportRealtimePublisher
{
    Task PublishChangedAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class SupportService(ISupportRepository repository, IStaffPermissionChecker permissions,
    TimeProvider clock, ISupportRealtimePublisher realtime) : ISupportService
{
    public async Task<bool> CanAccessAsync(Guid id, string actorId, CancellationToken ct)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actorId)) return false;
        var ownerId = await repository.OwnerIdAsync(id, ct);
        if (ownerId is null) return false;
        if (ownerId == actorId) return true;
        try { await permissions.EnsureAsync(actorId, StaffPermissions.ManageUsers, ct); return true; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public Task<SupportTicketDetails> CreateAsync(string userId, string subject, string body, CancellationToken ct)
    {
        RequireUser(userId);
        return repository.InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var ticket = SupportTicket.Create(userId, subject, body, now);
            repository.Add(ticket);
            foreach (var adminId in await repository.AdminIdsAsync(ct))
                if (adminId != userId) repository.AddNotification(Notice(adminId,
                    $"Yêu cầu hỗ trợ mới: {ticket.Subject}.", now));
            await repository.SaveAsync(ct);
            return await ToDetailsAsync(ticket, ct);
        }, ct);
    }

    public Task<IReadOnlyList<SupportTicketRow>> MineAsync(string userId, CancellationToken ct)
    { RequireUser(userId); return repository.ListAsync(userId, ct); }
    public async Task<IReadOnlyList<SupportTicketRow>> AdminQueueAsync(string adminId, CancellationToken ct)
    {
        await permissions.EnsureAsync(adminId, StaffPermissions.ManageUsers, ct);
        return await repository.ListAsync(null, ct);
    }
    public async Task<SupportTicketDetails> DetailsAsync(Guid id, string actorId, CancellationToken ct)
    {
        RequireUser(actorId);
        var ticket = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Yêu cầu hỗ trợ không tồn tại.");
        if (ticket.UserId != actorId)
            await permissions.EnsureAsync(actorId, StaffPermissions.ManageUsers, ct);
        return await ToDetailsAsync(ticket, ct);
    }
    public async Task<SupportTicketDetails> ReplyAsync(Guid id, string actorId, string body, CancellationToken ct)
    {
        RequireUser(actorId);
        var result = await repository.InTransactionAsync(async () =>
        {
            var ticket = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Yêu cầu hỗ trợ không tồn tại.");
            var isAdminReply = ticket.UserId != actorId;
            if (isAdminReply) await permissions.EnsureAsync(actorId, StaffPermissions.ManageUsers, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var message = ticket.Reply(actorId, body, now);
            repository.AddMessage(message);
            if (isAdminReply)
            {
                repository.AddNotification(Notice(ticket.UserId, $"CoCo đã trả lời yêu cầu: {ticket.Subject}.", now));
                repository.AddAudit(Audit(actorId, ticket.Id, "Support.Replied", now));
            }
            else foreach (var adminId in await repository.AdminIdsAsync(ct))
                repository.AddNotification(Notice(adminId, $"Khách hàng đã trả lời yêu cầu: {ticket.Subject}.", now));
            await repository.SaveAsync(ct);
            return await ToDetailsAsync(ticket, ct);
        }, ct);
        await realtime.PublishChangedAsync(id, CancellationToken.None);
        return result;
    }
    public async Task<SupportTicketDetails> CloseAsync(Guid id, string adminId, CancellationToken ct)
    {
        RequireUser(adminId);
        var result = await repository.InTransactionAsync(async () =>
        {
            await permissions.EnsureAsync(adminId, StaffPermissions.ManageUsers, ct);
            var ticket = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException("Yêu cầu hỗ trợ không tồn tại.");
            var now = clock.GetUtcNow().UtcDateTime;
            ticket.Close(now);
            repository.AddAudit(Audit(adminId, ticket.Id, "Support.Closed", now));
            if (ticket.UserId != adminId)
                repository.AddNotification(Notice(ticket.UserId, $"Yêu cầu hỗ trợ {ticket.Subject} đã đóng.", now));
            await repository.SaveAsync(ct);
            return await ToDetailsAsync(ticket, ct);
        }, ct);
        await realtime.PublishChangedAsync(id, CancellationToken.None);
        return result;
    }

    private async Task<SupportTicketDetails> ToDetailsAsync(SupportTicket ticket, CancellationToken ct)
    {
        var row = await repository.RowAsync(ticket.Id, ct) ??
            throw new KeyNotFoundException("Yêu cầu hỗ trợ không tồn tại.");
        return new SupportTicketDetails(row, ticket.Messages.OrderBy(x => x.CreatedAtUtc)
            .Select(x => new SupportMessageDto(x.Id, x.SenderId, x.Body, x.CreatedAtUtc)).ToList());
    }
    private static MarketplaceNotification Notice(string userId, string message, DateTime nowUtc) => new()
    { Id = Guid.NewGuid(), UserId = userId, Category = "Support", Message = message, CreatedAtUtc = nowUtc };
    private static AuditLog Audit(string actorId, Guid ticketId, string action, DateTime nowUtc) => new()
    { Id = Guid.NewGuid(), ActorId = actorId, EntityType = "SupportTicket", EntityId = ticketId.ToString(),
        Action = action, Details = action, CreatedAtUtc = nowUtc };
    private static void RequireUser(string userId)
    { if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Hãy đăng nhập."); }
}
