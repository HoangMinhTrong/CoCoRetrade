using System.Security.Claims;
using CoCoReTrade.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoCoReTrade.Web.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/wallet")]
public sealed class WalletController(IWalletService wallets) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]
    public async Task<ActionResult<WalletBalanceDto>> Balance(CancellationToken ct) =>
        Ok(await wallets.BalanceAsync(UserId, ct));
    [HttpPost("withdrawals")]
    public async Task<ActionResult<WithdrawalDto>> RequestWithdrawal(
        [FromBody] CreateWithdrawalRequest request, CancellationToken ct)
    {
        try
        {
            var result = await wallets.RequestWithdrawalAsync(UserId, request.RequestKey,
                request.Amount, request.BankAccountMasked, ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

public sealed record CreateWithdrawalRequest(Guid RequestKey, decimal Amount, string BankAccountMasked);
