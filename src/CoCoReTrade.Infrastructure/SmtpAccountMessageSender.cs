using System.Net;
using System.Net.Mail;
using CoCoReTrade.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoCoReTrade.Infrastructure;

public sealed class SmtpAccountMessageSender(IConfiguration configuration, IHostEnvironment environment,
    ILogger<SmtpAccountMessageSender> logger) : IAccountMessageSender
{
    public Task SendActivationAsync(string email, string link) => SendAsync(email, "Kích hoạt tài khoản CoCo", link);
    public Task SendPasswordResetAsync(string email, string link) => SendAsync(email, "Đặt lại mật khẩu CoCo", link);
    public Task SendOtpAsync(string email, string code, string purpose) =>
        SendAsync(email, purpose == "activation" ? "Mã kích hoạt CoCo" : "Mã đặt lại mật khẩu CoCo",
            $"Mã xác thực của bạn: {code}. Mã có hiệu lực 5 phút. Không chia sẻ mã này.");

    private async Task SendAsync(string email, string subject, string body)
    {
        if (environment.IsDevelopment())
        {
            logger.LogInformation("Development account message for {Email}: {Body}", email, body);
            return;
        }
        var host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP host is required.");
        var from = configuration["Smtp:From"] ?? throw new InvalidOperationException("SMTP sender is required.");
        using var client = new SmtpClient(host, int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(configuration["Smtp:User"], configuration["Smtp:Password"])
        };
        using var message = new MailMessage(from, email, subject, body);
        await client.SendMailAsync(message);
    }
}
