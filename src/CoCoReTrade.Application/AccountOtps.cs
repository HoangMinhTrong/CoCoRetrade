namespace CoCoReTrade.Application;

public sealed record IssuedAccountOtp(string Email, string Code);

public interface IAccountOtpService
{
    Task<IssuedAccountOtp> IssueActivationAsync(string userId, CancellationToken ct);
    Task<IssuedAccountOtp> IssueResetAsync(string userId, CancellationToken ct);
    Task<AccountActionResult> ConfirmActivationAsync(string userId, string code, CancellationToken ct);
    Task<AccountActionResult> VerifyResetAsync(string userId, string code, CancellationToken ct);
}
