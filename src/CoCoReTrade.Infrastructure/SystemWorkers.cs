using CoCoReTrade.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoCoReTrade.Infrastructure;

public sealed class OfferExpirationWorker(IServiceScopeFactory scopes,
    ILogger<OfferExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunAsync(stoppingToken);
    }
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var count = await scope.ServiceProvider.GetRequiredService<IOfferExpirationService>().ExpireDueAsync(100, ct);
            if (count > 0) logger.LogInformation("Đã hết hạn {OfferCount} đề xuất giá", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Xử lý hết hạn đề xuất giá thất bại"); }
    }
}

public sealed class OrderExpirationWorker(IServiceScopeFactory scopes,
    ILogger<OrderExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunAsync(stoppingToken);
    }
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var count = await scope.ServiceProvider.GetRequiredService<IOrderExpirationService>().ExpireUnpaidAsync(100, ct);
            if (count > 0) logger.LogInformation("Đã hủy {OrderCount} đơn chưa thanh toán quá hạn", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Xử lý hết hạn đơn hàng thất bại"); }
    }
}

public sealed class OutboxDispatcherWorker(IServiceScopeFactory scopes,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunAsync(stoppingToken);
    }
    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var count = await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>().DispatchAsync(50, ct);
            if (count > 0) logger.LogInformation("Đã gửi {OutboxCount} thông báo", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Xử lý hàng đợi thông báo thất bại"); }
    }
}

public sealed class ListingReminderWorker(IServiceScopeFactory scopes,
    ILogger<ListingReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunAsync(stoppingToken);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var count = await scope.ServiceProvider.GetRequiredService<IListingReminderService>()
                .SendDueAsync(100, ct);
            if (count > 0) logger.LogInformation("Đã gửi {ReminderCount} lời nhắc về sản phẩm", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Xử lý lời nhắc về sản phẩm thất bại"); }
    }
}
