using CoCoReTrade.Application;
using Microsoft.Extensions.Logging;

namespace CoCoReTrade.Infrastructure;

public sealed class OutboxDispatcher(IOutboxRepository repository,
    IEnumerable<IEmailNotificationProvider> emailProviders,
    IEnumerable<IWebPushNotificationProvider> pushProviders,
    TimeProvider clock, ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    public async Task<int> DispatchAsync(int batchSize, CancellationToken ct)
    {
        if (batchSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var email = emailProviders.FirstOrDefault();
        var push = pushProviders.FirstOrDefault();
        var sent = 0;
        for (var i = 0; i < batchSize; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = await repository.ClaimNextAsync(clock.GetUtcNow().UtcDateTime, ct);
            if (item is null) break;
            try
            {
                if (item.Channel == "email" && email is not null && !string.IsNullOrWhiteSpace(item.RecipientEmail))
                    await email.SendAsync(item.Id, item.RecipientEmail, item.Category, item.Message, ct);
                else if (item.Channel == "webpush" && push is not null)
                    await push.SendAsync(item.Id, item.UserId, item.Category, item.Message, ct);
                else
                {
                    await repository.DeferAsync(item.Id, item.LeaseToken,
                        clock.GetUtcNow().UtcDateTime.AddHours(1), "Provider unavailable", ct);
                    continue;
                }
                await repository.MarkSentAsync(item.Id, item.LeaseToken, clock.GetUtcNow().UtcDateTime, ct);
                sent++;
                logger.LogInformation("Dispatched outbox message {OutboxId} through {Channel}", item.Id, item.Channel);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                var delay = TimeSpan.FromMinutes(Math.Min(60, 1 << Math.Min(item.AttemptCount, 6)));
                await repository.DeferAsync(item.Id, item.LeaseToken,
                    clock.GetUtcNow().UtcDateTime.Add(delay), ex.GetType().Name, ct);
                logger.LogWarning(ex, "Outbox delivery {OutboxId} through {Channel} failed", item.Id, item.Channel);
            }
        }
        return sent;
    }
}
