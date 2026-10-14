namespace CoCoReTrade.Application;

public interface IListingReminderService
{
    Task<int> SendDueAsync(int batchSize, CancellationToken cancellationToken);
}
