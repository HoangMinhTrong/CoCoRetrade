using System.Net;
using System.Net.Mail;
using System.Text;
using CoCoReTrade.Application;
using Microsoft.Extensions.Configuration;

namespace CoCoReTrade.Infrastructure;

public sealed class SmtpAccountMessageSender(IConfiguration configuration) : IAccountMessageSender
{
    public Task SendActivationAsync(string email, string link) => SendAsync(email, "Kích hoạt tài khoản CoCo", link);
    public Task SendPasswordResetAsync(string email, string link) => SendAsync(email, "Đặt lại mật khẩu CoCo", link);
    public Task SendOtpAsync(string email, string code, string purpose) =>
        SendAsync(email, purpose == "activation" ? "Mã kích hoạt CoCo" : "Mã đặt lại mật khẩu CoCo",
            $"Mã xác thực của bạn: {code}. Mã có hiệu lực 5 phút. Không chia sẻ mã này.");

    private async Task SendAsync(string email, string subject, string body)
    {
        var host = configuration["Smtp:Host"];
        var from = configuration["Smtp:From"];
        var user = configuration["Smtp:User"];
        var password = configuration["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from) ||
            string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Cần cấu hình đầy đủ Smtp:Host, Smtp:From, Smtp:User và Smtp:Password để gửi mã qua email.");
        using var client = new SmtpClient(host, int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, password)
        };
        using var message = new MailMessage(from, email, subject, body)
        {
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        await client.SendMailAsync(message);
    }
}
