using Entities.Model;
using MassTransit;
using OutboxApi.Services;

namespace OutboxApi.Messaging
{
    public class OutboxConsumer : IConsumer<Outbox>
    {
        private readonly IOutboxService _outboxService;
        public OutboxConsumer(IOutboxService outboxService)
        {
            _outboxService = outboxService;
        }

        public async Task Consume(ConsumeContext<Outbox> context)
        {
            try
            {
                var outbox = context.Message;
                await _outboxService.AddNewOutboxAsync(outbox);
            }
            catch (Exception)
            {
                throw; // Rethrow to trigger MassTransit retry/error handling
            }
        }
    }
}
