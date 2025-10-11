namespace OutboxApi.Repository
{
    public interface IOutboxRepository
    {
        Task AddNewOutboxAsync(Entities.Model.Outbox outbox);
        Task<List<Entities.Model.Outbox>> GetUnprocessedOutboxesAsync();
        Task MarkOutboxAsProcessedAsync(Guid outboxId);
        Task MarkOutboxAsFailedAsync(Guid outboxId);
    }
}
