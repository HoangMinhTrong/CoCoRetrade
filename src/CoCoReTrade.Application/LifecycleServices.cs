using CoCoReTrade.Domain;

namespace CoCoReTrade.Application;

public interface IProductLifecycleService
{
    Task<Inspection> InspectAsync(string actorId, Product product, InspectionSubmission request,
        string? sku, IReadOnlyCollection<ProductImage> images, DateTime nowUtc, CancellationToken ct);
    Task PublishAsync(string actorId, Product product, DateTime nowUtc, CancellationToken ct);
    Task RejectAsync(string actorId, Product product, string reason, CancellationToken ct);
    void ReserveForOrder(Product product, Order order);
}

public sealed class ProductLifecycleService(IStaffPermissionChecker permissions) : IProductLifecycleService
{
    public async Task<Inspection> InspectAsync(string actorId, Product product, InspectionSubmission request,
        string? sku, IReadOnlyCollection<ProductImage> images, DateTime nowUtc, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Inspect, ct);
        return product.RecordInspection(actorId, request.Passed, request.Name, request.Brand, request.Category,
            request.Size, request.OriginalPrice, request.AskingPrice, request.VerifiedCondition,
            request.Notes, sku, images, nowUtc);
    }
    public async Task PublishAsync(string actorId, Product product, DateTime nowUtc, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Publish, ct);
        product.Publish(nowUtc);
    }
    public async Task RejectAsync(string actorId, Product product, string reason, CancellationToken ct)
    {
        await permissions.EnsureAsync(actorId, StaffPermissions.Reject, ct);
        product.Reject(reason);
    }
    public void ReserveForOrder(Product product, Order order)
    {
        if (order.ProductId != product.Id || order.Status != OrderStatus.PendingProcessing ||
            order.PaymentStatus != PaymentStatus.Unpaid)
            throw new InvalidOperationException("Only a newly created unpaid order can reserve its product.");
        product.Reserve();
    }
}

public interface IOrderLifecycleService
{
    void MarkReady(Order order);
    void ConfirmPayment(Order order, string adminId, string reference, DateTime nowUtc);
    void Complete(Order order, Product product, DateTime nowUtc);
    void Cancel(Order order, Product product, bool isAdmin, DateTime nowUtc);
}

public sealed class OrderLifecycleService : IOrderLifecycleService
{
    public void MarkReady(Order order) => order.MarkReady();
    public void ConfirmPayment(Order order, string adminId, string reference, DateTime nowUtc) =>
        order.ConfirmPayment(adminId, reference, nowUtc);
    public void Complete(Order order, Product product, DateTime nowUtc) => order.Complete(product, nowUtc);
    public void Cancel(Order order, Product product, bool isAdmin, DateTime nowUtc) =>
        order.Cancel(product, isAdmin, nowUtc);
}
