using Entities.Model;
using MassTransit;
using OutboxApi.Services;

namespace OutboxApi.Messaging
{
    public class OutboxConsumer : IConsumer<Outbox>
    {
        private readonly IOutboxService _outboxService;
        private readonly ILogger<OutboxConsumer> _logger;

        public OutboxConsumer(IOutboxService outboxService, ILogger<OutboxConsumer> logger)
        {
            _outboxService = outboxService;
            _logger = logger;
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
