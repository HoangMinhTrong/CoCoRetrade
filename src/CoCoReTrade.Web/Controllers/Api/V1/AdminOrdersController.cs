using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

public sealed record PaymentConfirmation(string? Reference);
public sealed record ShippingHandoffRequest(string? TrackingCode);
public sealed record ReturnedDeliveryConfirmation(bool ItemReturnedToStore);

[ApiController]
[Authorize(Policy = StaffPermissions.ManageOrders)]
[Route("api/v1/admin/orders")]
public sealed class AdminOrdersController(IAdminOrderService orders) : ControllerBase
{
    private string AdminId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> Index(CancellationToken ct) => Ok(await orders.QueueAsync(AdminId, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Details(Guid id, CancellationToken ct)
    {
        var order = await orders.DetailsAsync(id, AdminId, ct);
        return order is null ? NotFound() : Ok(order);
    }
    [HttpPost("{id:guid}/ready")]
    public Task<ActionResult<OrderDto>> Ready(Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ShippingHandoffRequest? request,
        CancellationToken ct) =>
        Execute(() => orders.MarkReadyAsync(id, AdminId, ct, request?.TrackingCode));
    [HttpPost("{id:guid}/confirm")]
    public Task<ActionResult<OrderDto>> Confirm(Guid id, CancellationToken ct) =>
        Execute(() => orders.ConfirmAsync(id, AdminId, ct));
    [HttpPost("{id:guid}/confirm-payment")]
    public Task<ActionResult<OrderDto>> ConfirmPayment(Guid id, [FromBody] PaymentConfirmation request, CancellationToken ct) =>
        Execute(() => orders.ConfirmPaymentAsync(id, AdminId, request.Reference, ct));
    [HttpPost("{id:guid}/complete")]
    public Task<ActionResult<OrderDto>> Complete(Guid id, CancellationToken ct) =>
        Execute(() => orders.CompleteAsync(id, AdminId, ct));
    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken ct) =>
        Execute(() => orders.CancelAsync(id, AdminId, ct));
    [HttpPost("{id:guid}/return-undelivered")]
    public Task<ActionResult<OrderDto>> ReturnUndelivered(Guid id, [FromBody] ReturnedDeliveryConfirmation request, CancellationToken ct) =>
        Execute(() => orders.ReturnUndeliveredAsync(id, AdminId, request.ItemReturnedToStore, ct));
    private async Task<ActionResult<OrderDto>> Execute(Func<Task<OrderDto>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}
