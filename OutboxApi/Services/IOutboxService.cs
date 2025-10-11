namespace OutboxApi.Services
{
    public interface IOutboxService
    {
        Task AddNewOutboxAsync(Entities.Model.Outbox outbox);
        Task<List<Entities.Model.Outbox>> GetUnprocessedOutboxesAsync();
        Task MarkOutboxAsProcessedAsync(Guid outboxId);
        Task MarkOutboxAsFailedAsync(Guid outboxId);
    }
}
