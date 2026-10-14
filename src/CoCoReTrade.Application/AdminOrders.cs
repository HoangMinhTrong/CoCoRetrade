namespace CoCoReTrade.Application;

public interface IAdminOrderService
{
    Task<IReadOnlyList<OrderDto>> QueueAsync(string actorId, CancellationToken ct);
    Task<OrderDto?> DetailsAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> MarkReadyAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> ConfirmPaymentAsync(Guid id, string actorId, string reference, CancellationToken ct);
    Task<OrderDto> CompleteAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> CancelAsync(Guid id, string actorId, CancellationToken ct);
}

public sealed class AdminOrderService(IOrderService orders, IStaffPermissionChecker permissions) : IAdminOrderService
{
    public async Task<IReadOnlyList<OrderDto>> QueueAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.AdminQueueAsync(ct);
    }
    public async Task<OrderDto?> DetailsAsync(Guid id, string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.DetailsAsync(id, actorId, true, ct);
    }
    public async Task<OrderDto> MarkReadyAsync(Guid id, string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.MarkReadyAsync(id, actorId, ct);
    }
    public async Task<OrderDto> ConfirmPaymentAsync(Guid id, string actorId, string reference, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.ConfirmPaymentAsync(id, actorId, reference, ct);
    }
    public async Task<OrderDto> CompleteAsync(Guid id, string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.CompleteAsync(id, actorId, ct);
    }
    public async Task<OrderDto> CancelAsync(Guid id, string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.CancelAsync(id, actorId, true, ct);
    }
}
