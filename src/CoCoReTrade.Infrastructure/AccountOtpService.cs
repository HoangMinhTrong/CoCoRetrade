using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CoCoReTrade.Application;
using CoCoReTrade.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoCoReTrade.Infrastructure;

public sealed class AccountOtpService(AppDbContext db, UserManager<ApplicationUser> users,
    TimeProvider clock) : IAccountOtpService
{
    private static readonly PasswordHasher<AccountOtp> Hasher = new();
    public Task<IssuedAccountOtp> IssueActivationAsync(string userId, CancellationToken ct) => IssueAsync(userId, "activation", ct);
    public Task<IssuedAccountOtp> IssueResetAsync(string userId, CancellationToken ct) => IssueAsync(userId, "reset", ct);
    public Task<AccountActionResult> ConfirmActivationAsync(string userId, string code, CancellationToken ct) =>
        VerifyAsync(userId, code, "activation", ct);
    public Task<AccountActionResult> VerifyResetAsync(string userId, string code, CancellationToken ct) =>
        VerifyAsync(userId, code, "reset", ct);

    private async Task<IssuedAccountOtp> IssueAsync(string userId, string purpose, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await users.FindByIdAsync(userId) ?? throw new KeyNotFoundException("Tài khoản không tồn tại.");
        if (purpose == "activation" && user.EmailConfirmed)
            throw new InvalidOperationException("Tài khoản đã kích hoạt.");
        if (purpose == "reset" && !user.EmailConfirmed)
            throw new InvalidOperationException("Tài khoản chưa kích hoạt.");
        var now = clock.GetUtcNow().UtcDateTime;
        var previous = await db.AccountOtps.Where(x => x.UserId == userId && x.Purpose == purpose &&
            x.ConsumedAtUtc == null).OrderByDescending(x => x.CreatedAtUtc).Take(5).ToListAsync(ct);
        if (previous.Any(x => x.CreatedAtUtc > now.AddSeconds(-60)))
            throw new InvalidOperationException("Vui lòng chờ 60 giây trước khi yêu cầu mã mới.");
        foreach (var old in previous) old.ConsumedAtUtc = now;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new AccountOtp { UserId = userId, Purpose = purpose,
            CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(5) };
        otp.CodeHash = Hasher.HashPassword(otp, code);
        db.AccountOtps.Add(otp);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new IssuedAccountOtp(user.Email ?? throw new InvalidOperationException("Email is required."), code);
    }

    private async Task<AccountActionResult> VerifyAsync(string userId, string code, string purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || code is null || !Regex.IsMatch(code, @"^[0-9]{6}$"))
            return new(false, "Mã xác thực phải gồm 6 chữ số.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var otp = await db.AccountOtps.Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (otp is null || otp.ExpiresAtUtc <= now || otp.Attempts >= 5)
            return new(false, "Mã đã hết hạn hoặc vượt quá số lần thử. Hãy yêu cầu mã mới.");
        if (Hasher.VerifyHashedPassword(otp, otp.CodeHash, code) == PasswordVerificationResult.Failed)
        {
            otp.Attempts++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(false, "Mã xác thực không đúng.");
        }
        var user = await users.FindByIdAsync(userId);
        if (user is null) return new(false, "Tài khoản không tồn tại.");
        otp.ConsumedAtUtc = now;
        if (purpose == "activation")
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var confirmed = await users.ConfirmEmailAsync(user, token);
            if (!confirmed.Succeeded) return new(false, "Không thể kích hoạt tài khoản.");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(true);
        }
        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true, Token: resetToken, UserId: user.Id);
    }
}
