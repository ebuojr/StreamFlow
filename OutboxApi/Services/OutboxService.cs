using Entities.Model;
using OutboxApi.Repository;

namespace OutboxApi.Services
{
    public class OutboxService : IOutboxService
    {
        private readonly IOutboxRepository _outboxRepository;
        public OutboxService(IOutboxRepository outboxRepository)
        {
            _outboxRepository = outboxRepository;
        }
        public async Task AddNewOutboxAsync(Outbox outbox)
        {
            await _outboxRepository.AddNewOutboxAsync(outbox);
        }

        public async Task<List<Outbox>> GetUnprocessedOutboxesAsync()
        {
            return await _outboxRepository.GetUnprocessedOutboxesAsync();
        }

        public async Task MarkOutboxAsFailedAsync(Guid outboxId)
        {
            await _outboxRepository.MarkOutboxAsFailedAsync(outboxId);
        }

        public async Task MarkOutboxAsProcessedAsync(Guid outboxId)
        {
            await _outboxRepository.MarkOutboxAsProcessedAsync(outboxId);
        }
    }
}
