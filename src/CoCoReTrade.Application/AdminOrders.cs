using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public sealed record HeldPickupOrderDto(Guid OrderId, Guid ProductId, string ProductSku,
    string ProductName, string RecipientName, string RecipientPhone, DateTime? PickupAtUtc,
    DateTime? PickupDeadlineAtUtc, OrderStatus Status, bool UsesAcceptedOffer, decimal TotalAmount);

public interface IAdminHeldPickupRepository
{
    Task<IReadOnlyList<HeldPickupOrderDto>> ListAsync(CancellationToken ct);
}

public interface IAdminOrderService
{
    Task<IReadOnlyList<OrderDto>> QueueAsync(string actorId, CancellationToken ct);
    Task<IReadOnlyList<HeldPickupOrderDto>> HeldPickupsAsync(string actorId, CancellationToken ct);
    Task<OrderDto?> DetailsAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> MarkReadyAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> ConfirmPaymentAsync(Guid id, string actorId, string reference, CancellationToken ct);
    Task<OrderDto> CompleteAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> CancelAsync(Guid id, string actorId, CancellationToken ct);
    Task<OrderDto> ReturnUndeliveredAsync(Guid id, string actorId, bool itemReturnedToStore, CancellationToken ct);
}

public sealed class AdminOrderService(IOrderService orders, IStaffPermissionChecker permissions,
    IAdminHeldPickupRepository heldPickups) : IAdminOrderService
{
    public async Task<IReadOnlyList<OrderDto>> QueueAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.AdminQueueAsync(ct);
    }
    public async Task<IReadOnlyList<HeldPickupOrderDto>> HeldPickupsAsync(string actorId, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await heldPickups.ListAsync(ct);
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
    public async Task<OrderDto> ReturnUndeliveredAsync(Guid id, string actorId, bool itemReturnedToStore, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.ManageOrders, ct);
        return await orders.ReturnUndeliveredAsync(id, actorId, itemReturnedToStore, ct);
    }
}
