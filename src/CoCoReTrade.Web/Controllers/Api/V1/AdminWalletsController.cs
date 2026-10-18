using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize(Policy = StaffPermissions.ManageWallets)]
[Route("api/v1/admin/wallets")]
public sealed class AdminWalletsController(IAdminWalletService wallets, IWalletService ledger) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminWalletRow>>> Index(CancellationToken ct) =>
        Ok(await wallets.ListAsync(ActorId, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminWalletDetails>> Details(Guid id, CancellationToken ct)
    {
        var wallet = await wallets.DetailsAsync(ActorId, id, ct);
        return wallet is null ? NotFound() : Ok(wallet);
    }
    [HttpGet("withdrawals")]
    public async Task<ActionResult<IReadOnlyList<AdminWithdrawalRow>>> Withdrawals(CancellationToken ct) =>
        Ok(await wallets.WithdrawalsAsync(ActorId, ct));
    [HttpGet("payout-candidates")]
    public async Task<ActionResult<IReadOnlyList<SellerPayoutCandidate>>> PayoutCandidates(CancellationToken ct) =>
        Ok(await wallets.PayoutCandidatesAsync(ActorId, ct));
    [HttpPost("payouts/{orderId:guid}")]
    public async Task<ActionResult<AdminWalletDetails>> Credit(Guid orderId, [FromBody] ConfirmPayoutRequest request, CancellationToken ct)
    {
        try { return Ok(await wallets.CreditCompletedOrderAsync(ActorId, orderId, request.Confirmed, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
    [HttpPost("withdrawals/{id:guid}/approve")]
    public async Task<ActionResult<WithdrawalDto>> Approve(Guid id, CancellationToken ct)
    {
        try { return Ok(await ledger.DecideWithdrawalAsync(ActorId, id, true, null, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
    [HttpPost("withdrawals/{id:guid}/reject")]
    public async Task<ActionResult<WithdrawalDto>> Reject(Guid id, [FromBody] RejectWithdrawalRequest request, CancellationToken ct)
    {
        try { return Ok(await ledger.DecideWithdrawalAsync(ActorId, id, false, request.Reason, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
    [HttpPost("withdrawals/{id:guid}/confirm-transfer")]
    public async Task<ActionResult<WithdrawalDto>> ConfirmTransfer(Guid id,
        [FromBody] ConfirmWithdrawalTransferRequest request, CancellationToken ct)
    {
        if (!request.Confirmed) return BadRequest(new { error = "Hãy xác nhận đã chuyển khoản thực tế trước." });
        try { return Ok(await ledger.ConfirmTransferAsync(ActorId, id, request.TransferReference, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

public sealed record ConfirmPayoutRequest(bool Confirmed);
public sealed record RejectWithdrawalRequest(string Reason);
public sealed record ConfirmWithdrawalTransferRequest(string TransferReference, bool Confirmed);
