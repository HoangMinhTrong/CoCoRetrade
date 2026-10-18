using System.Net;
using System.Net.Mail;
using CoCoReTrade.Application;
using Microsoft.Extensions.Configuration;

namespace CoCoReTrade.Infrastructure;

public sealed class SmtpMarketplaceEmailProvider(IConfiguration configuration) : IEmailNotificationProvider
{
    public async Task SendAsync(Guid messageId, string recipientEmail, string category, string message, CancellationToken ct)
    {
        var host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("Chưa cấu hình máy chủ SMTP.");
        var from = configuration["Smtp:From"] ?? throw new InvalidOperationException("Chưa cấu hình địa chỉ gửi SMTP.");
        using var client = new SmtpClient(host, int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(configuration["Smtp:User"], configuration["Smtp:Password"])
        };
        using var mail = new MailMessage(from, recipientEmail, $"CoCo ReTrade: {category}", message);
        mail.Headers.Add("X-CoCo-Outbox-Id", messageId.ToString("N"));
        await client.SendMailAsync(mail, ct);
    }
}
