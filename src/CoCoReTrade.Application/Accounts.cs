namespace CoCoReTrade.Application;

public sealed record RegisterAccount(string FullName, string Email, string Phone, string Password);
public sealed record AccountActionResult(bool Succeeded, string? Error = null, string? Token = null, string? UserId = null, string? Email = null);

public interface IAccountService
{
    Task<AccountActionResult> RegisterAsync(RegisterAccount request);
    Task<AccountActionResult> ConfirmEmailAsync(string userId, string token);
    Task<AccountActionResult> LoginAsync(string identifier, string password, bool rememberMe);
    Task LogoutAsync();
    Task<AccountActionResult> CreateResetTokenAsync(string identifier);
    Task<AccountActionResult> FindUnconfirmedAsync(string identifier);
    Task<AccountActionResult> ResetPasswordAsync(string userId, string token, string newPassword);
    Task<AccountActionResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}

public interface IAccountMessageSender
{
    Task SendActivationAsync(string email, string link);
    Task SendPasswordResetAsync(string email, string link);
    Task SendOtpAsync(string email, string code, string purpose);
}
